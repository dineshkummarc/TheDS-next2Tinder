using System;
using System.Collections.Generic;

public static class CppTarget
{
	// From http://en.cppreference.com/w/cpp/keyword
	private static readonly HashSet<string> reservedWords = new HashSet<string> {
		// Keywords
		"alignas",
		"aliasof",
		"and",
		"and_eq",
		"asm",
		"auto",
		"bitand",
		"bitor",
		"bool",
		"break",
		"case",
		"catch",
		"char",
		"char16_t",
		"char32_t",
		"class",
		"compl",
		"const",
		"constexpr",
		"const_cast",
		"continue",
		"decltype",
		"default",
		"delete",
		"do",
		"double",
		"dynamic_cast",
		"else",
		"enum",
		"explicit",
		"export",
		"extern",
		"false",
		"float",
		"for",
		"friend",
		"goto",
		"if",
		"inline",
		"int",
		"long",
		"mutable",
		"namespace",
		"new",
		"noexcept",
		"not",
		"not_eq",
		"nullptr",
		"operator",
		"or",
		"or_eq",
		"private",
		"protected",
		"public",
		"register",
		"reinterpret_cast",
		"return",
		"short",
		"signed",
		"sizeof",
		"static",
		"static_assert",
		"static_cast",
		"struct",
		"switch",
		"template",
		"this",
		"thread_local",
		"throw",
		"true",
		"try",
		"typedef",
		"typeid",
		"typename",
		"union",
		"unsigned",
		"using",
		"virtual",
		"void",
		"volatile",
		"wchar_t",
		"while",
		"xor",
		"xor_eq",
		
		// Other
		"std",
		"vector",
		"string",
	};
	
	public static string Generate(Module module)
	{
		module.Accept(new RenameSymbolsPass(reservedWords, false));
		return module.Accept(new CppTargetVisitor());
	}
}

public class CppTargetVisitor : Visitor<string>
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
		
		{ BinaryOp.Equal, "==" },
		{ BinaryOp.NotEqual, "!=" },
		{ BinaryOp.LessThan, "<" },
		{ BinaryOp.GreaterThan, ">" },
		{ BinaryOp.LessThanEqual, "<=" },
		{ BinaryOp.GreaterThanEqual, ">=" },
	};
	
	// From http://en.cppreference.com/w/cpp/language/operator_precedence
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
	
	private void Indent()
	{
		indent += "  ";
	}
	
	private void Dedent()
	{
		indent = indent.Substring(2);
	}
	
	private string TypeToString(Type type, string name)
	{
		if (type is ClassType) {
			return ((ClassType)type).def.name + " *" + (name ?? "");
		}
		if (type is PrimType) {
			if (((PrimType)type).kind == PrimKind.String) {
				return "std::string" + (name != null ? " " + name : "");
			}
			return type.ToString() + (name != null ? " " + name : "");
		}
		if (type is ListType) {
			return "std::vector<" + TypeToString(type.ItemType(), null) + "> *" + (name ?? "");
		}
		if (type is VoidType) {
			return "void" + (name != null ? " " + name : "");
		}
		if (type is FuncType) {
			FuncType funcType = (FuncType)type;
			return TypeToString(funcType.returnType, "(*" + (name ?? "") + ")" + "(" +
				funcType.argTypes.ConvertAll(x => TypeToString(x, null)).Join(", ") + ")");
		}
		if (type is NullableType) {
			type = ((NullableType)type).type;
			if (type is PrimType) {
				if (((PrimType)type).kind == PrimKind.String) {
					return "std::string" + " *" + (name ?? "");
				}
				return type.ToString() + " *" + (name ?? "");
			}
			return TypeToString(type, name);
		}
		throw new NotImplementedException();
	}
	
	public override string Visit(Block node)
	{
		string text = "{\n";
		Indent();
		text += node.stmts.ConvertAll(x => x.Accept(this)).Join();
		Dedent();
		return text + indent + "}";
	}

	public override string Visit(Module node)
	{
		return "#include <string>\n#include <vector>\n" + node.block.stmts.ConvertAll(x => x.Accept(this)).Join();
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
		string text = "";
		foreach (Stmt stmt in node.block.stmts)
			text += stmt.Accept(this);
		return text;
	}
	
	public override string Visit(WhileStmt node)
	{
		return indent + "while (" + node.test.Accept(this).StripParens() + ") " + node.block.Accept(this) + "\n";
	}
	
	public override string Visit(VarDef node)
	{
		string text = indent;
		if (node.info.inExternal && node.info.classDef == null) {
			text += "extern ";
		}
		text += TypeToString(node.symbol.type, node.symbol.finalName);
		if (node.value != null && node.info.funcDef != null) {
			text += " = " + node.value.Accept(this).StripParens();
		}
		text += ";\n";
		return text;
	}

	public override string Visit(FuncDef node)
	{
		string text = indent + (node.isStatic ? "static " : "") + TypeToString(node.returnType.computedType.InstanceType(), node.symbol.finalName +
			"(" + node.argDefs.ConvertAll(x => TypeToString(x.type.computedType.InstanceType(), x.symbol.finalName)).Join(", ") + ")");
		if (node.block != null) {
			text += " " + node.block.Accept(this) + "\n";
		} else {
			text += ";\n";
		}
		return text;
	}
	
	public override string Visit(ClassDef node)
	{
		List<string> initializers = new List<string>();
		string text = indent + "struct " + node.symbol.finalName + " {\n";
		Indent();
		foreach (Stmt stmt in node.block.stmts) {
			text += stmt.Accept(this);
			if (stmt is VarDef) {
				VarDef varDef = (VarDef)stmt;
				initializers.Add(varDef.symbol.finalName + "(" + (varDef.value == null ? "" : varDef.value.Accept(this)) + ")");
			}
		}
		if (initializers.Count > 0 && !node.info.inExternal) {
			// Generate constructor
			text += indent + node.symbol.finalName + "() : " + initializers.Join(", ") + " {}\n";
		}
		Dedent();
		return text + indent + "};\n";
	}
	
	public override string Visit(VarExpr node)
	{
		throw new NotImplementedException();
	}
	
	public override string Visit(NullExpr node)
	{
		return "NULL";
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
		return "std::string(" + node.value.ToQuotedString() + ")";
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
		string text = "new std::vector<" + TypeToString(node.computedType.ItemType(), null) + ">()";
		foreach (Expr item in node.items) {
			text = "append(" + text + ", " + item.Accept(this).StripParens() + ")";
		}
		return text;
	}
	
	public override string Visit(UnaryExpr node)
	{
		return "(" + unaryOpToString[node.op] + node.value.Accept(this) + ")";
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
		if (node.left is BinaryExpr && precedence >= binaryOpPrecedence[((BinaryExpr)node.left).op]) {
			left = left.StripParens();
		}
		if (node.right is BinaryExpr && precedence >= binaryOpPrecedence[((BinaryExpr)node.right).op]) {
			right = right.StripParens();
		}
		if (node.op == BinaryOp.Add && node.left.computedType.IsString() && node.right is StringExpr) {
			// Don't wrap string literals in std::string() if we don't need to
			right = ((StringExpr)node.right).value.ToQuotedString();
		}
		return "(" + left + " " + binaryOpToString[node.op] + " " + right + ")";
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
		Type targetType = node.target.computedType.InstanceType();
		string value = node.value.Accept(this);

		// Primitive => nullable primitive is a new
		if (targetType is NullableType) {
			Type type = ((NullableType)targetType).type;
			if (type is PrimType && node.value.computedType is PrimType) {
				return "new " + TypeToString(type, null) + "(" + (node.value is StringExpr ? ((StringExpr)node.value).value.ToQuotedString() :
					value.StripParens()) + ")";
			}
		}

		// Nullable primitive => primitive is a dereference
		bool isDereference = false;
		if (node.value.computedType is NullableType) {
			Type type = ((NullableType)node.value.computedType).type;
			if (type is PrimType && targetType is PrimType) {
				isDereference = true;
			}
		}
		value = (isDereference ? "*" + value : value.StripParens());

		// Don't insert trivial casts
		if (node.value.computedType is NullableType && node.computedType.EqualsType(((NullableType)node.value.computedType).type)) {
			return value;
		}

		return "static_cast<" + TypeToString(node.target.computedType.InstanceType(), null) + ">(" + value + ")";
	}
	
	public override string Visit(MemberExpr node)
	{
		string separator;
		if (node.obj.computedType is ClassType || node.obj.computedType is NullableType) {
			separator = "->";
		} else if (node.obj.computedType is MetaType) {
			separator = "::";
		} else {
			separator = ".";
		}
		return node.obj.Accept(this) + separator + node.symbol.finalName;
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
