using System;
using System.Collections.Generic;

public static class JsTarget
{
	// From https://developer.mozilla.org/en/JavaScript/Reference/Reserved_Words
	// and https://developer.mozilla.org/en/JavaScript/Reference/Global_Objects
	private static readonly HashSet<string> reservedWords = new HashSet<string> {
		// Reserved words
		"break",
		"case",
		"catch",
		"continue",
		"debugger",
		"default",
		"delete",
		"do",
		"else",
		"finally",
		"for",
		"function",
		"if",
		"in",
		"instanceof",
		"new",
		"return",
		"switch",
		"this",
		"throw",
		"try",
		"typeof",
		"var",
		"void",
		"while",
		"with",

		// Words reserved for possible future use
		"class",
		"enum",
		"export",
		"extends",
		"import",
		"super",
		"implements",
		"interface",
		"let",
		"package",
		"private",
		"protected",
		"public",
		"static",
		"yield",
		"const",
		
		// Standard global objects
		"Array",
		"ArrayBuffer",
		"Boolean",
		"Date",
		"decodeURI",
		"decodeURIComponent",
		"encodeURI",
		"encodeURIComponent",
		"Error",
		"eval",
		"EvalError",
		"Float32Array",
		"Float64Array",
		"Function",
		"Infinity",
		"Int16Array",
		"Int32Array",
		"Int8Array",
		"isFinite",
		"isNaN",
		"Iterator",
		"JSON",
		"Math",
		"NaN",
		"Number",
		"Object",
		"parseFloat",
		"parseInt",
		"RangeError",
		"ReferenceError",
		"RegExp",
		"StopIteration",
		"String",
		"SyntaxError",
		"TypeError",
		"Uint16Array",
		"Uint32Array",
		"Uint8Array",
		"Uint8ClampedArray",
		"undefined",
		"uneval",
		"URIError",
		
		// Other
		"constructor",
		"prototype",
		"null",
		"true",
		"false",
	};

	public static string Generate(Module module)
	{
		module.Accept(new RenameSymbolsPass(reservedWords, true));
		return module.Accept(new JsTargetVisitor());
	}
}

public class JsTargetVisitor : Visitor<string>
{
	private static readonly Dictionary<UnaryOp, string> unaryOpToString = new Dictionary<UnaryOp, string> {
		{ UnaryOp.Negative, "-" },
		{ UnaryOp.Not, "!" },
	};
	private static readonly Dictionary<BinaryOp, string> binaryOpToString = new Dictionary<BinaryOp, string> {
		{ BinaryOp.Assign, "=" },
		
		{ BinaryOp.And, "&&" },
		{ BinaryOp.Or, "||" },
		
		{ BinaryOp.Add, "+" },
		{ BinaryOp.Subtract, "-" },
		{ BinaryOp.Multiply, "*" },
		{ BinaryOp.Divide, "/" },
		
		{ BinaryOp.LShift, "<<" },
		{ BinaryOp.RShift, ">>" },
		{ BinaryOp.BitAnd, "&" },
		{ BinaryOp.BitOr, "|" },
		{ BinaryOp.BitXor, "^" },
		
		{ BinaryOp.Equal, "===" },
		{ BinaryOp.NotEqual, "!==" },
		{ BinaryOp.LessThan, "<" },
		{ BinaryOp.GreaterThan, ">" },
		{ BinaryOp.LessThanEqual, "<=" },
		{ BinaryOp.GreaterThanEqual, ">=" },
	};
	
	// From https://developer.mozilla.org/en/JavaScript/Reference/Operators/Operator_Precedence
	private static readonly Dictionary<BinaryOp, int> binaryOpPrecedence = new Dictionary<BinaryOp, int> {
		{ BinaryOp.Multiply, 5 },
		{ BinaryOp.Divide, 5 },
		{ BinaryOp.Add, 6 },
		{ BinaryOp.Subtract, 6 },
		{ BinaryOp.LShift, 7 },
		{ BinaryOp.RShift, 7 },
		{ BinaryOp.LessThan, 8 },
		{ BinaryOp.LessThanEqual, 8 },
		{ BinaryOp.GreaterThan, 8 },
		{ BinaryOp.GreaterThanEqual, 8 },
		{ BinaryOp.Equal, 9 },
		{ BinaryOp.NotEqual, 9 },
		{ BinaryOp.BitAnd, 10 },
		{ BinaryOp.BitOr, 11 },
		{ BinaryOp.BitXor, 12 },
		{ BinaryOp.And, 13 },
		{ BinaryOp.Or, 14 },
		{ BinaryOp.Assign, 16 },
	};
	private string indent = "";
	private string prefix = "";
	
	private void Indent()
	{
		indent += "  ";
	}
	
	private void Dedent()
	{
		indent = indent.Substring(2);
	}
	
	public override string Visit(Block node)
	{
		string text = "{\n";
		Indent();
		string oldPrefix = prefix;
		prefix = "";
		text += node.stmts.ConvertAll(x => x.Accept(this)).Join();
		prefix = oldPrefix;
		Dedent();
		return text + indent + "}";
	}

	public override string Visit(Module node)
	{
		return "\"use strict\";\n" + node.block.stmts.ConvertAll(x => x.Accept(this)).Join();
	}

	public override string Visit(IfStmt node)
	{
		string text = indent;
		while (true) {
			text += "if (" + node.test.Accept(this).StripParens() + ") " + node.thenBlock.Accept(this);
			if (node.elseBlock == null) {
				text += "\n";
				break;
			}
			text += " else ";
			if (node.elseBlock.stmts.Count == 1 && node.elseBlock.stmts[0] is IfStmt) {
				node = (IfStmt)node.elseBlock.stmts[0];
			} else {
				text += node.elseBlock.Accept(this) + "\n";
				break;
			}
		}
		return text;
	}

	public override string Visit(ReturnStmt node)
	{
		return indent + "return" + (node.value == null ? "" : " " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(ExprStmt node)
	{
		return indent + node.value.Accept(this).StripParens() + ";\n";
	}
	
	public override string Visit(ExternalStmt node)
	{
		return "";
	}
	
	public override string Visit(WhileStmt node)
	{
		return indent + "while (" + node.test.Accept(this).StripParens() + ") " + node.block.Accept(this) + "\n";
	}
	
	private string DefineVar(Def node)
	{
		return (prefix.Length > 0 ? prefix : "var ") + node.symbol.finalName;
	}
	
	public override string Visit(VarDef node)
	{
		return indent + DefineVar(node) + (node.value == null ? "" : " = " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(FuncDef node)
	{
		return indent + DefineVar(node) + " = function(" + node.argDefs.ConvertAll(x => x.symbol.finalName).Join(", ") +
			") " + node.block.Accept(this) + ";\n";
	}
	
	public override string Visit(ClassDef node)
	{
		// Write out the class constructor
		string text = indent + DefineVar(node) + " = function() {\n";
		Indent();
		foreach (Stmt stmt in node.block.stmts) {
			if (stmt is VarDef) {
				VarDef varDef = (VarDef)stmt;
				text += indent + "this." + varDef.symbol.finalName + " = " + (varDef.value == null ?
					"null" : varDef.value.Accept(this).StripParens()) + ";\n";
			}
		}
		Dedent();
		text += indent + "};\n";
		
		// Write out members
		string oldPrefix = prefix;
		foreach (Stmt stmt in node.block.stmts) {
			if (!(stmt is VarDef)) {
				bool isStatic = (!(stmt is FuncDef) || ((FuncDef)stmt).isStatic);
				prefix = oldPrefix + node.symbol.finalName + (isStatic ? "." : ".prototype.");
				text += stmt.Accept(this);
			}
		}
		prefix = oldPrefix;
		return text;
	}
	
	public override string Visit(VarExpr node)
	{
		throw new NotImplementedException();
	}
	
	public override string Visit(NullExpr node)
	{
		return "null";
	}

	public override string Visit(ThisExpr node)
	{
		return "this";
	}

	public override string Visit(BoolExpr node)
	{
		return node.value ? "true" : "false";
	}
	
	public override string Visit(IntExpr node)
	{
		return node.value.ToString();
	}

	public override string Visit(FloatExpr node)
	{
		return node.value.ToString();
	}

	public override string Visit(StringExpr node)
	{
		return node.value.ToQuotedString();
	}
	
	public override string Visit(IdentExpr node)
	{
		return node.symbol.finalName;
	}
	
	public override string Visit(TypeExpr node)
	{
		throw new NotImplementedException();
	}
	
	public override string Visit(ListExpr node)
	{
		return "[" + node.items.ConvertAll(x => x.Accept(this).StripParens()).Join(", ") + "]";
	}
	
	public override string Visit(UnaryExpr node)
	{
		return "(" + unaryOpToString[node.op] + node.value.Accept(this) + ")";
	}
	
	private static int Precedence(BinaryExpr node)
	{
		if (node.op == BinaryOp.Divide && node.computedType.IsInt()) {
			return binaryOpPrecedence[BinaryOp.BitOr];
		}
		return binaryOpPrecedence[node.op];
	}
	
	public override string Visit(BinaryExpr node)
	{
		// Strip parentheses if they aren't needed
		string left = node.left.Accept(this);
		string right = node.right.Accept(this);
		if (node.op == BinaryOp.NullableDefault) {
			return "TODO(" + left.StripParens() + ", " + right.StripParens() + ")";
		}
		int precedence = binaryOpPrecedence[node.op];
		if (node.left is BinaryExpr && precedence >= Precedence((BinaryExpr)node.left)) {
			left = left.StripParens();
		}
		if (node.right is BinaryExpr && precedence >= Precedence((BinaryExpr)node.right)) {
			right = right.StripParens();
		}
		string text = left + " " + binaryOpToString[node.op] + " " + right;
		if (node.op == BinaryOp.Divide && node.computedType.IsInt()) {
			return "(" + text + " | 0)";
		} else {
			return "(" + text + ")";
		}
	}

	public override string Visit(CallExpr node)
	{
		return (node.isCtor ? "new " : "") + node.func.Accept(this) + "(" +
			node.args.ConvertAll(x => x.Accept(this).StripParens()).Join(", ") + ")";
	}
	
	public override string Visit(ParamExpr node)
	{
		throw new NotImplementedException();
	}
	
	public override string Visit(CastExpr node)
	{
		if (node.value.computedType.IsFloat() && node.target.computedType.InstanceType().IsInt()) {
			return "(" + node.value.Accept(this) + " | 0)";
		}
		return node.value.Accept(this);
	}
	
	public override string Visit(MemberExpr node)
	{
		return node.obj.Accept(this) + "." + node.symbol.finalName;
	}
	
	public override string Visit(IndexExpr node)
	{
		return node.obj.Accept(this) + "[" + node.index.Accept(this) + "]";
	}
	
	public override string Visit(NullableExpr node)
	{
		throw new NotImplementedException();
	}
}
