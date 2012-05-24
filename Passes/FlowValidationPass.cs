using System;
using System.IO;
using System.Collections.Generic;

// This automatically writes all graphs in the compiled program to "graph.dot"
// for debugging. To visualize the graph, run the following command:
//
//     dot bin/Debug/graphs.dot -Tpng -ograph.png; open graph.png
//
public class FlowValidationPass : DefaultVisitor
{
	private readonly FlowGraphBuilder builder = new FlowGraphBuilder();
	private readonly FlowGraphAnalyzer analyzer = new FlowGraphAnalyzer();
	private readonly Log log;
	private bool inFunc;

	public FlowValidationPass(Log log)
	{
		this.log = log;
	}

	public override Null Visit(Module node)
	{
		node.Accept(builder);
		analyzer.Run(builder);
		File.WriteAllText("graphs.dot", builder.ToDotGraph());
		base.Visit(node);
		return null;
	}

	public override Null Visit(Block node)
	{
		// Check for dead code (code not reached by any flow)
		Scope old = scope;
		scope = node.scope;
		foreach (Stmt stmt in node.stmts) {
			FlowNode flowNode;
			if (inFunc && builder.nodeMap.TryGetValue(stmt, out flowNode) && flowNode.knowledge == null) {
				log.WarningDeadCode(stmt.location);
				break;
			}
			stmt.Accept(this);
		}
		scope = old;

		return null;
	}

	public override Null Visit(FuncDef node)
	{
		inFunc = true;
		base.Visit(node);
		inFunc = false;

		// Check for a return value (flow must not reach the end of non-void functions)
		if (node.block != null && !(node.symbol.type.ReturnType() is VoidType) && builder.nodeMap[node].knowledge != null) {
			log.ErrorNotAllPathsReturnValue(node.location);
		}

		return null;
	}

	public override Null Visit(CastExpr node)
	{
		base.Visit(node);

		// Check for provably invalid dereferences of local variables
		if (node.value is IdentExpr) {
			IdentExpr identExpr = (IdentExpr)node.value;
			if (identExpr.symbol.def.info.funcDef != null) {
				FlowNode flowNode = builder.nodeMap[node];
				if (flowNode.knowledge != null) {
					IsNull isNull = flowNode.knowledge.isNull.GetOrDefault(identExpr.symbol, IsNull.Maybe);
					if (isNull == IsNull.Yes) {
						log.WarningNullDereference(node.location, identExpr.name);
					} else if (isNull == IsNull.Maybe) {
						log.WarningNullableDereference(node.location, identExpr.name);
					}
					return null;
				}
			}
			
			// Be conservative and warn about all other dereferences
			if ((node.value.computedType is NullableType) && !(node.computedType is NullableType)) {
				log.WarningNullableDereference(node.location, identExpr.name);
				return null;
			}
		}

		// Be conservative and warn about all other dereferences
		if ((node.value.computedType is NullableType) && !(node.computedType is NullableType)) {
			log.WarningNullableDereference(node.location, null);
		}

		return null;
	}
}

// Constructed so that intersection is "&" and union is "|"
public enum IsNull
{
	No = 1,
	Yes = 2,
	Maybe = 3,
	Unknown = 0,
}

// A node in the CFG that would be built if the AST were compiled to basic
// blocks. This graph contains additional nodes involving flow, such as
// the CheckNode node which restricts the nullability of local variables.
public class FlowNode
{
	public readonly List<FlowNode> next;
	public Knowledge knowledge;

	public FlowNode(params FlowNode[] next)
	{
		this.next = new List<FlowNode>(next);
	}

	public virtual bool Update(Knowledge knowledge)
	{
		return true;
	}

	public override string ToString()
	{
		return "(none)";
	}
}

// An assignment to a local variable
public class AssignNode : FlowNode
{
	public readonly Symbol symbol;
	public readonly IsNull isNull;

	public AssignNode(Symbol symbol, IsNull isNull, FlowNode next) : base(next)
	{
		this.symbol = symbol;
		this.isNull = isNull;
	}

	public override bool Update(Knowledge knowledge)
	{
		// Overwrite the type with the assigned type
		knowledge.isNull[symbol] = isNull;
		return true;
	}

	public override string ToString()
	{
		return "set " + symbol.def.name + " to " + isNull.AsString();
	}
}

// An assignment to a local variable from another local variable
public class AliasNode : FlowNode
{
	public readonly Symbol leftSymbol;
	public readonly Symbol rightSymbol;

	public AliasNode(Symbol leftSymbol, Symbol rightSymbol, FlowNode next) : base(next)
	{
		this.leftSymbol = leftSymbol;
		this.rightSymbol = rightSymbol;
	}

	public override bool Update(Knowledge knowledge)
	{
		// Overwrite the type with the assigned type
		knowledge.isNull[leftSymbol] = knowledge.isNull.GetOrDefault(rightSymbol, IsNull.Maybe);
		return true;
	}

	public override string ToString()
	{
		return "set " + leftSymbol.def.name + " to " + rightSymbol.def.name;
	}
}

// A "==" or "!=" check against null
public class CheckNode : FlowNode
{
	public readonly Symbol symbol;
	public readonly IsNull isNull;

	public CheckNode(Symbol symbol, IsNull isNull, FlowNode next) : base(next)
	{
		this.symbol = symbol;
		this.isNull = isNull;
	}

	public override bool Update(Knowledge knowledge)
	{
		// Narrow to more specific knowledge
		IsNull intersection = isNull & knowledge.isNull.GetOrDefault(symbol, IsNull.Maybe);
		knowledge.isNull[symbol] = intersection;

		// A contradiction means flow out of this node is impossible
		return (intersection != IsNull.Unknown);
	}

	public override string ToString()
	{
		return symbol.def.name + " is " + isNull.AsString();
	}
}

// Return statements block flow
public class BlockerNode : FlowNode
{
	public BlockerNode(FlowNode next) : base(next)
	{
	}

	public override bool Update(Knowledge knowledge)
	{
		// Always indicate no flow
		return false;
	}

	public override string ToString()
	{
		return "block";
	}
}

// The nullability knowledge that we have at any one point in the program
public class Knowledge
{
	public readonly Dictionary<Symbol, IsNull> isNull = new Dictionary<Symbol, IsNull>();

	public Knowledge Clone()
	{
		Knowledge clone = new Knowledge();
		clone.isNull.AddRange(isNull);
		return clone;
	}

	public void UnionWith(Knowledge other)
	{
		HashSet<Symbol> symbols = new HashSet<Symbol>();
		symbols.AddRange(isNull.Keys);
		symbols.AddRange(other.isNull.Keys);
		foreach (Symbol symbol in symbols) {
			isNull[symbol] = isNull.GetOrDefault(symbol, IsNull.Unknown) | other.isNull.GetOrDefault(symbol, IsNull.Unknown);
		}
	}

	public override string ToString()
	{
		return string.Join(", ", isNull.Items().ConvertAll(pair => {
			return pair.Key.def.name + " is " + pair.Value.AsString();
		}).ToArray());
	}

	public override bool Equals(object obj)
	{
		Knowledge other = (Knowledge)obj;
		IsNull value;
		foreach (KeyValuePair<Symbol, IsNull> pair in isNull) {
			if (!other.isNull.TryGetValue(pair.Key, out value) || value != pair.Value) {
				return false;
			}
		}
		foreach (KeyValuePair<Symbol, IsNull> pair in other.isNull) {
			if (!isNull.TryGetValue(pair.Key, out value) || value != pair.Value) {
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		int hashCode = 0;
		foreach (KeyValuePair<Symbol, IsNull> pair in isNull) {
			hashCode ^= pair.Key.GetHashCode() ^ pair.Value.GetHashCode();
		}
		return hashCode;
	}
}

// A tuple of FlowNodes that has either one or two elements. One element
// indicates that flow is linear and two elements indicates flow is branched.
public class FlowPair
{
	public readonly HashSet<FlowNode> nodes;
	private FlowNode first, second;

	public FlowPair()
	{
		this.nodes = new HashSet<FlowNode>();
	}

	public void Clear()
	{
		first = second = null;
	}

	public FlowNode Get()
	{
		// Merge multiple next flow nodes to make sure there is only one next flow node
		if (second != null) {
			first = new FlowNode(first, second);
			second = null;
			nodes.Add(first);
		}
		return first;
	}

	public FlowNode GetTrue()
	{
		return first;
	}

	public FlowNode GetFalse()
	{
		return second ?? first;
	}

	public void Set(FlowNode next)
	{
		nodes.Add(next);
		first = next;
		second = null;
	}

	public void Set(FlowNode trueNode, FlowNode falseNode)
	{
		nodes.Add(trueNode);
		nodes.Add(falseNode);
		first = trueNode;
		second = falseNode;
	}
}

// A visitor that builds the flow graph that would be built if the AST were
// compiled to basic blocks
public class FlowGraphBuilder : DefaultVisitor
{
	public readonly Dictionary<Node, FlowNode> nodeMap = new Dictionary<Node, FlowNode>();
	public readonly List<FlowNode> roots = new List<FlowNode>();
	public readonly FlowPair next = new FlowPair();

	public override Null Visit(Block node)
	{
		// Reverse visitation order because flow is calculated bottom to top
		Scope old = scope;
		scope = node.scope;
		for (int i = node.stmts.Count - 1; i >= 0; i--) {
			Stmt stmt = node.stmts[i];
			stmt.Accept(this);
			if (!nodeMap.ContainsKey(stmt)) {
				nodeMap.Add(stmt, next.Get());
			}
		}
		scope = old;
		return null;
	}

	public override Null Visit(UnaryExpr node)
	{
		base.Visit(node);

		// Invert the flow for boolean not
		if (node.op == UnaryOp.Not) {
			if (next.GetTrue() != next.GetFalse()) {
				next.Set(new FlowNode(next.GetFalse(), next.GetTrue()));
			}
		}

		return null;
	}

	public override Null Visit(CastExpr node)
	{
		base.Visit(node);
		
		// Remember the current flow node for later
		nodeMap.Add(node, next.GetTrue());

		return null;
	}

	public override Null Visit(BinaryExpr node)
	{
		if (node.op == BinaryOp.And || node.op == BinaryOp.Or) {
			FlowNode endTrue = next.GetTrue();
			FlowNode endFalse = next.GetFalse();

			// Calculate flow for the right expression
			node.right.Accept(this);
			FlowNode right = next.Get();

			// Apply the short-circuit logic
			if (node.op == BinaryOp.And) {
				next.Set(right, endFalse);
			} else {
				next.Set(endTrue, right);
			}

			// Calculate flow for the left expression
			node.left.Accept(this);
		} else if (node.op == BinaryOp.Equal || node.op == BinaryOp.NotEqual) {
			base.Visit(node);
			
			// Find the function-local symbol being compared with null, if any
			IdentExpr identExpr;
			if (node.left is IdentExpr && node.right is CastExpr && ((CastExpr)node.right).value is NullExpr) {
				identExpr = (IdentExpr)node.left;
			} else if (node.right is IdentExpr && node.left is CastExpr && ((CastExpr)node.left).value is NullExpr) {
				identExpr = (IdentExpr)node.right;
			} else {
				return null;
			}
			if (identExpr.symbol.def.info.funcDef != null) {
				IsNull isNull = (node.op == BinaryOp.Equal) ? IsNull.Yes : IsNull.No;
				next.Set(
					new CheckNode(identExpr.symbol, isNull, next.GetTrue()),
					new CheckNode(identExpr.symbol, isNull ^ IsNull.Maybe, next.GetFalse())
				);
			}
		} else if (node.op == BinaryOp.Assign) {
			// Check for assignment to a local variable
			if (node.left is IdentExpr) {
				IdentExpr identExpr = (IdentExpr)node.left;
				HandleAssignment(identExpr.symbol, node.right);
			}

			base.Visit(node);
		} else {
			base.Visit(node);
		}

		return null;
	}

	public override Null Visit(ReturnStmt node)
	{
		// Return statements stop flow completely
		next.Set(new BlockerNode(next.Get()));
		next.Set(new FlowNode(next.Get()));
		
		base.Visit(node);

		return null;
	}

	public override Null Visit(IfStmt node)
	{
		// Calculate flow for the else branch, adding another
		// node at the end for analysis inside the branch
		FlowNode end = next.Get();
		next.Set(new FlowNode(end));
		if (node.elseBlock != null) {
			node.elseBlock.Accept(this);
		}
		FlowNode elseNode = next.Get();

		// Calculate flow for the then branch, adding another
		// node at the end for analysis inside the branch
		next.Set(new FlowNode(end));
		node.thenBlock.Accept(this);
		FlowNode thenNode = next.Get();

		// Calculate flow for the test expression
		next.Set(thenNode, elseNode);
		node.test.Accept(this);

		return null;
	}

	public override Null Visit(WhileStmt node)
	{
		// Create an flow node for the back edge
		FlowNode end = next.Get();
		FlowNode loop = new FlowNode();

		// Calculate flow for the body block
		next.Set(loop);
		node.block.Accept(this);
		FlowNode body = next.Get();

		// Calculate flow for the test expression
		next.Set(body, end);
		node.test.Accept(this);

		// Link the back edge to before the test expression
		loop.next.Add(next.Get());

		return null;
	}

	public override Null Visit(ExternalStmt node)
	{
		// There is no flow to validate in external blocks
		return null;
	}

	public override Null Visit(VarDef node)
	{
		base.Visit(node);
		HandleAssignment(node.symbol, node.value);
		return null;
	}

	public override Null Visit(FuncDef node)
	{
		// Make a new end node to start off the flow
		FlowNode root = new FlowNode();
		nodeMap.Add(node, root);
		next.Set(root);
		node.block.Accept(this);
		for (int i = node.argDefs.Count - 1; i >= 0; i--) {
			node.argDefs[i].Accept(this);
		}
		roots.Add(next.Get());
		return null;
	}

	public string ToDotGraph()
	{
		// Generate a graph in GraphViz format
		Dictionary<FlowNode, int> ids = new Dictionary<FlowNode, int>();
		int nextID = 0;
		string text = "digraph {\n";
		foreach (FlowNode node in next.nodes) {
			int id = ids[node] = nextID++;
			string label = "action: " + node.ToString() + "\n" + (node.knowledge != null ? node.knowledge.ToString() : "impossible");
			text += "  n" + id + " [label = " + label.ToQuotedString() + "];\n";
		}
		foreach (FlowNode node in next.nodes) {
			foreach (FlowNode nextNode in node.next) {
				text += "  n" + ids[node] + " -> n" + ids[nextNode] + ";\n";
			}
		}
		return text + "}\n";
	}
	
	private void HandleAssignment(Symbol symbol, Expr node)
	{
		if (symbol.def.info.funcDef != null) {
			// Handle assigning a variable to an assignment
			if (node is BinaryExpr) {
				BinaryExpr binaryExpr = (BinaryExpr)node;
				if (binaryExpr.op == BinaryOp.Assign) {
					HandleAssignment(symbol, binaryExpr.left);
					return;
				}
			}

			// Handle assigning a variable to another variable
			if (node is IdentExpr) {
				IdentExpr identExpr = (IdentExpr)node;
				if (identExpr.symbol.def.info.funcDef != null) {
					next.Set(new AliasNode(symbol, identExpr.symbol, next.Get()));
					return;
				}
			}

			// Handle assignment to a casted value
			if (node is CastExpr) {
				HandleAssignment(symbol, ((CastExpr)node).value);
				return;
			}

			// Handle regular assignment
			Type type = (node != null) ? node.computedType : symbol.type;
			IsNull isNull;
			if (type is NullableType) {
				isNull = IsNull.Maybe;
			} else if (type is NullType) {
				isNull = IsNull.Yes;
			} else {
				isNull = IsNull.No;
			}
			next.Set(new AssignNode(symbol, isNull, next.Get()));
		}
	}
}

public class FlowGraphAnalyzer
{
	private class Result
	{
		public readonly FlowNode node;
		public readonly Knowledge knowledge;

		public Result(FlowNode node, Knowledge knowledge)
		{
			this.node = node;
			this.knowledge = knowledge;
		}

		public override bool Equals(object obj)
		{
			Result other = (Result)obj;
			return node == other.node && knowledge.Equals(other.knowledge);
		}

		public override int GetHashCode()
		{
			return node.GetHashCode() + knowledge.GetHashCode();
		}
	}

	private HashSet<Result> results = new HashSet<Result>();

	private void Visit(FlowNode node, Knowledge knowledge)
	{
		// Memoize the results so we don't do more work than necessary. This is
		// also required to handle loops without hanging.
		Result result = new Result(node, knowledge);
		if (results.Contains(result)) {
			return;
		}
		results.Add(result);

		// Add to the knowledge flowing through this path, but stop if flow stops
		knowledge = knowledge.Clone();
		if (!node.Update(knowledge)) {
			return;
		}

		// Merge that knowledge into the total knowledge for this node
		if (node.knowledge != null) {
			node.knowledge.UnionWith(knowledge);
		} else {
			node.knowledge = knowledge.Clone();
		}

		// Propagate flow to next links
		foreach (FlowNode next in node.next) {
			Visit(next, knowledge);
		}
	}

	public void Run(FlowGraphBuilder builder)
	{
		// Analyze all the function roots
		foreach (FlowNode root in builder.roots) {
			Visit(root, new Knowledge());
		}
	}
}
