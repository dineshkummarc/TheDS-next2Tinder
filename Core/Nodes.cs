using System;
using System.Collections.Generic;

////////////////////////////////////////////////////////////////////////////////
// NodeInfo
////////////////////////////////////////////////////////////////////////////////

public class NodeInfo
{
	public Module module;
	public ClassDef classDef;
	public FuncDef funcDef;
	public bool inFuncArgList;
	public bool inExternal;
	public bool inStaticFunc;
	public bool inCtor;
	
	public NodeInfo Clone() {
		return new NodeInfo {
			module = module,
			classDef = classDef,
			funcDef = funcDef,
			inFuncArgList = inFuncArgList,
			inExternal = inExternal,
			inStaticFunc = inStaticFunc,
			inCtor = inCtor,
		};
	}
}

////////////////////////////////////////////////////////////////////////////////
// Node
////////////////////////////////////////////////////////////////////////////////

public abstract class Node
{
	public Location location;
	public NodeInfo info;
	
	public abstract T Accept<T>(Visitor<T> visitor);
}

public class Block : Node
{
	public List<Stmt> stmts;
	public Scope scope;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class Module : Node
{
	public string name;
	public Block block;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

////////////////////////////////////////////////////////////////////////////////
// Stmt
////////////////////////////////////////////////////////////////////////////////

public abstract class Stmt : Node
{
}

public class IfStmt : Stmt
{
	public Expr test;
	public Block thenBlock;
	public Block elseBlock; // Might be null

	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ReturnStmt : Stmt
{
	public Expr value; // Might be null

	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ExprStmt : Stmt
{
	public Expr value;

	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ExternalStmt : Stmt
{
	public Block block;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class WhileStmt : Stmt
{
	public Expr test;
	public Block block;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

////////////////////////////////////////////////////////////////////////////////
// Def
////////////////////////////////////////////////////////////////////////////////

public abstract class Def : Stmt
{
	public string name;
	public Symbol symbol;
}

public class VarDef : Def
{
	public Expr type;
	public Expr value; // Might be null
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class FuncDef : Def
{
	public bool isStatic;
	public Expr returnType;
	public List<VarDef> argDefs;
	public Block block; // Might be null

	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ClassDef : Def
{
	public Block block;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

////////////////////////////////////////////////////////////////////////////////
// Expr
////////////////////////////////////////////////////////////////////////////////

public abstract class Expr : Node
{
	public Type computedType;
}

public class VarExpr : Expr
{
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class NullExpr : Expr
{
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ThisExpr : Expr
{
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class BoolExpr : Expr
{
	public bool value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class IntExpr : Expr
{
	public int value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class FloatExpr : Expr
{
	public float value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class StringExpr : Expr
{
	public string value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class IdentExpr : Expr
{
	public string name;
	public Symbol symbol;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class TypeExpr : Expr
{
	public Type type;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ListExpr : Expr
{
	public List<Expr> items;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public enum UnaryOp
{
	Not,
	Negative,
}

public class UnaryExpr : Expr
{
	public UnaryOp op;
	public Expr value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public enum BinaryOp
{
	Assign,
	
	NullableDefault,
	
	And,
	Or,
	
	Add,
	Subtract,
	Multiply,
	Divide,
	
	LShift,
	RShift,
	BitAnd,
	BitOr,
	BitXor,
	
	Equal,
	NotEqual,
	LessThan,
	GreaterThan,
	LessThanEqual,
	GreaterThanEqual,
}

public class BinaryExpr : Expr
{
	public Expr left;
	public BinaryOp op;
	public Expr right;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class CallExpr : Expr
{
	public bool isCtor;
	public Expr func;
	public List<Expr> args;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class ParamExpr : Expr
{
	public Expr type;
	public List<Expr> typeParams;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class CastExpr : Expr
{
	public Expr value;
	public Expr target;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class MemberExpr : Expr
{
	public Expr obj;
	public string name;
	public bool isSafeDereference;
	public Symbol symbol;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class IndexExpr : Expr
{
	public Expr obj;
	public Expr index;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

public class NullableExpr : Expr
{
	public Expr value;
	
	public override T Accept<T>(Visitor<T> visitor)
	{
		return visitor.Visit(this);
	}
}

////////////////////////////////////////////////////////////////////////////////
// Visitor
////////////////////////////////////////////////////////////////////////////////

public abstract class Visitor<T>
{
	public void VisitAll<N>(List<N> nodes) where N : Node
	{
		nodes.ForEach(n => n.Accept(this));
	}
	
	public abstract T Visit(Block node);

	public abstract T Visit(Module node);

	public abstract T Visit(IfStmt node);

	public abstract T Visit(ReturnStmt node);

	public abstract T Visit(ExprStmt node);
	
	public abstract T Visit(ExternalStmt node);
	
	public abstract T Visit(WhileStmt node);
	
	public abstract T Visit(VarDef node);

	public abstract T Visit(FuncDef node);

	public abstract T Visit(ClassDef node);
	
	public abstract T Visit(VarExpr node);
	
	public abstract T Visit(NullExpr node);

	public abstract T Visit(ThisExpr node);

	public abstract T Visit(BoolExpr node);
	
	public abstract T Visit(IntExpr node);

	public abstract T Visit(FloatExpr node);

	public abstract T Visit(StringExpr node);
	
	public abstract T Visit(IdentExpr node);
	
	public abstract T Visit(TypeExpr node);
	
	public abstract T Visit(ListExpr node);
	
	public abstract T Visit(UnaryExpr node);
	
	public abstract T Visit(BinaryExpr node);

	public abstract T Visit(CallExpr node);
	
	public abstract T Visit(ParamExpr node);
	
	public abstract T Visit(CastExpr node);
	
	public abstract T Visit(MemberExpr node);
	
	public abstract T Visit(IndexExpr node);
	
	public abstract T Visit(NullableExpr node);
}

// We need a way of representing no return value and void can't be used as
// a generic type parameter in C#. System.Void could be used for this
// purpose except it's explicitly disallowed by the C# compiler.
public sealed class Null
{
	private Null()
	{
	}
}

public class DefaultVisitor : Visitor<Null>
{
	public Scope scope;
	
	public override Null Visit(Block node)
	{
		Scope old = scope;
		scope = node.scope;
		VisitAll(node.stmts);
		scope = old;
		return null;
	}

	public override Null Visit(Module node)
	{
		node.block.Accept(this);
		return null;
	}

	public override Null Visit(IfStmt node)
	{
		node.test.Accept(this);
		node.thenBlock.Accept(this);
		if (node.elseBlock != null) {
			node.elseBlock.Accept(this);
		}
		return null;
	}

	public override Null Visit(ReturnStmt node)
	{
		if (node.value != null) {
			node.value.Accept(this);
		}
		return null;
	}

	public override Null Visit(ExprStmt node)
	{
		node.value.Accept(this);
		return null;
	}
	
	public override Null Visit(ExternalStmt node)
	{
		node.block.Accept(this);
		return null;
	}
	
	public override Null Visit(WhileStmt node)
	{
		node.test.Accept(this);
		node.block.Accept(this);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		node.type.Accept(this);
		if (node.value != null) {
			node.value.Accept(this);
		}
		return null;
	}

	public override Null Visit(FuncDef node)
	{
		node.returnType.Accept(this);
		VisitAll(node.argDefs);
		if (node.block != null) {
			node.block.Accept(this);
		}
		return null;
	}

	public override Null Visit(ClassDef node)
	{
		node.block.Accept(this);
		return null;
	}
	
	public override Null Visit(VarExpr node)
	{
		return null;
	}
	
	public override Null Visit(NullExpr node)
	{
		return null;
	}

	public override Null Visit(ThisExpr node)
	{
		return null;
	}

	public override Null Visit(BoolExpr node)
	{
		return null;
	}
	
	public override Null Visit(IntExpr node)
	{
		return null;
	}

	public override Null Visit(FloatExpr node)
	{
		return null;
	}

	public override Null Visit(StringExpr node)
	{
		return null;
	}
	
	public override Null Visit(IdentExpr node)
	{
		return null;
	}
	
	public override Null Visit(TypeExpr node)
	{
		return null;
	}
	
	public override Null Visit(ListExpr node)
	{
		VisitAll(node.items);
		return null;
	}
	
	public override Null Visit(UnaryExpr node)
	{
		node.value.Accept(this);
		return null;
	}
	
	public override Null Visit(BinaryExpr node)
	{
		node.left.Accept(this);
		node.right.Accept(this);
		return null;
	}
	
	public override Null Visit(CallExpr node)
	{
		node.func.Accept(this);
		VisitAll(node.args);
		return null;
	}
	
	public override Null Visit(ParamExpr node)
	{
		node.type.Accept(this);
		VisitAll(node.typeParams);
		return null;
	}
	
	public override Null Visit(CastExpr node)
	{
		node.value.Accept(this);
		node.target.Accept(this);
		return null;
	}
	
	public override Null Visit(MemberExpr node)
	{
		node.obj.Accept(this);
		return null;
	}
	
	public override Null Visit(IndexExpr node)
	{
		node.obj.Accept(this);
		node.index.Accept(this);
		return null;
	}
	
	public override Null Visit(NullableExpr node)
	{
		node.value.Accept(this);
		return null;
	}
}
