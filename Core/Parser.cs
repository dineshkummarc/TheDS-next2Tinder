using System;
using System.Collections.Generic;

// A pratt parser is a parser that associates semantics with tokens instead of
// with grammar rules. Pratt parsers simplify and speed up expression parsing
// when many operators with varying precedence levels are involved.
public class PrattParser
{
	public class Symbol
	{
		// The left binding power controls the precedence level of the symbol
		// when the left denotation is triggered
		public int leftBindingPower;
		
		// Used for operands and unary prefix operators ("nud" in classic
		// Pratt parser terminology)
		public Func<ParserContext, Expr> prefixParser;
		
		// Used for binary infix operators and unary postfix operators
		// ("led" in classic Pratt parser terminology)
		public Func<ParserContext, Expr, Expr> infixParser;
	}
	
	private Dictionary<TokenKind, Symbol> table = new Dictionary<TokenKind, Symbol>();
	
	// Attempt to parse an expression from the provided parser context at the
	// provided binding power (precedence level). Returns an object on success
	// or null on failure.
	public Expr Parse(ParserContext context, int rightBindingPower = 0)
	{
		Symbol symbol;
		if (!table.TryGetValue(context.CurrentToken().kind, out symbol) || symbol.prefixParser == null) {
			return null;
		}
		Expr left = symbol.prefixParser(context);
		while (left != null) {
			if (!table.TryGetValue(context.CurrentToken().kind, out symbol) || symbol.infixParser == null ||
			    	rightBindingPower >= symbol.leftBindingPower) {
				break;
			}
			left = symbol.infixParser(context, left);
		}
		return left;
	}
	
	// Return the symbol that matches tokens of the given kind, creating it if
	// needed. The binding power of the returned symbol will be at least as
	// high as the provided binding power, but may be higher due to a previous
	// Get() call.
	public Symbol Get(TokenKind kind, int bindingPower = 0)
	{
		Symbol symbol;
		if (!table.TryGetValue(kind, out symbol)) {
			table.Add(kind, symbol = new Symbol());
		}
		symbol.leftBindingPower = Math.Max(symbol.leftBindingPower, bindingPower);
		return symbol;
	}
	
	// Create a literal operand that returns the result of applying func to the
	// matched token.
	public Symbol Literal(TokenKind kind, Func<ParserContext, Token, Expr> func)
	{
		Symbol symbol = Get(kind);
		symbol.prefixParser = (ParserContext context) => {
			Token token = context.CurrentToken();
			context.Next();
			return func(context, token);
		};
		return symbol;
	}
	
	// Create a binary infix operator with a certain binding power (precedence
	// level) that returns the result of applying func to the left expression,
	// the token, and the right expression.
	public Symbol Infix(TokenKind kind, int bindingPower, Func<ParserContext, Expr, Token, Expr, Expr> func, bool rightAssociative)
	{
		Symbol symbol = Get(kind, bindingPower);
		symbol.infixParser = (ParserContext context, Expr left) => {
			Token token = context.CurrentToken();
			context.Next();
			Expr right = Parse(context, bindingPower - (rightAssociative ? 1 : 0));
			return right != null ? func(context, left, token, right) : null;
		};
		return symbol;
	}
	
	// Create a binary infix operator with a certain binding power (precedence
	// level) that returns the result of applying func to the left expression,
	// the token, and the right expression.
	public Symbol Prefix(TokenKind kind, int bindingPower, Func<ParserContext, Token, Expr, Expr> func)
	{
		Symbol symbol = Get(kind);
		symbol.prefixParser = (ParserContext context) => {
			Token token = context.CurrentToken();
			context.Next();
			Expr right = Parse(context, bindingPower);
			return right != null ? func(context, token, right) : null;
		};
		return symbol;
	}
}

public class ParserContext
{
	private int index;
	private List<Token> tokens;
	private Stack<NodeInfo> stack = new Stack<NodeInfo>();
	
	public ParserContext(List<Token> tokens)
	{
		this.tokens = tokens;
		stack.Push(new NodeInfo());
	}
	
	public Token CurrentToken()
	{
		return tokens[index];
	}
	
	public int CurrentIndex()
	{
		return index;
	}
	
	public NodeInfo Info()
	{
		return stack.Peek();
	}
	
	public NodeInfo PushInfo()
	{
		NodeInfo info = Info().Clone();
		stack.Push(info);
		return info;
	}
	
	public void PopInfo()
	{
		stack.Pop();
	}
	
	public bool Peek(TokenKind kind)
	{
		if (tokens[index].kind == kind) {
			return true;
		}
		return false;
	}
	
	public bool Consume(TokenKind kind)
	{
		if (Peek(kind)) {
			Next();
			return true;
		}
		return false;
	}
	
	public void Next()
	{
		if (index + 1 < tokens.Count) {
			index++;
		}
	}
}

public static class Parser
{
	private static readonly PrattParser pratt = new PrattParser();
	
	private static T Wrap<T>(ParserContext context, Token token, T node) where T : Node
	{
		node.location = (token ?? context.CurrentToken()).location;
		node.info = context.Info();
		return node;
	}
	
	static Parser()
	{
		// Literals
		pratt.Literal(TokenKind.Var, (context, token) => Wrap(context, token, new VarExpr()));
		pratt.Literal(TokenKind.Null, (context, token) => Wrap(context, token, new NullExpr()));
		pratt.Literal(TokenKind.This, (context, token) => Wrap(context, token, new ThisExpr()));
		pratt.Literal(TokenKind.True, (context, token) => Wrap(context, token, new BoolExpr { value = true }));
		pratt.Literal(TokenKind.False, (context, token) => Wrap(context, token, new BoolExpr { value = false }));
		pratt.Literal(TokenKind.StringLit, (context, token) => Wrap(context, token, new StringExpr { value = token.text }));
		pratt.Literal(TokenKind.Identifier, (context, token) => Wrap(context, token, new IdentExpr { name = token.text }));
		pratt.Literal(TokenKind.CharLit, (context, token) => Wrap(context, token, new IntExpr { value = token.text[0] }));
		pratt.Get(TokenKind.IntLit).prefixParser = ParseIntExpr;
		pratt.Get(TokenKind.FloatLit).prefixParser = ParseFloatExpr;
		
		// Types
		pratt.Literal(TokenKind.Void, (context, token) => Wrap(context, token, new TypeExpr { type = new VoidType() }));
		pratt.Literal(TokenKind.List, (context, token) => Wrap(context, token, new TypeExpr { type = new ListType() }));
		pratt.Literal(TokenKind.Function, (context, token) => Wrap(context, token, new TypeExpr { type = new FuncType() }));
		pratt.Literal(TokenKind.Bool, ParsePrimType);
		pratt.Literal(TokenKind.Int, ParsePrimType);
		pratt.Literal(TokenKind.Float, ParsePrimType);
		pratt.Literal(TokenKind.String, ParsePrimType);
		
		// Infix operators
		foreach (OperatorInfo<BinaryOp> info in Constants.binaryOperators)
			pratt.Infix(info.kind, info.precedence, ParseBinaryExpr, Constants.rightAssociativeOperators.Contains(info.kind));
		
		// Prefix operators
		foreach (OperatorInfo<UnaryOp> info in Constants.unaryOperators)
			pratt.Prefix(info.kind, info.precedence, ParsePrefixExpr);
		
		// Parsers requiring special behavior
		pratt.Get(TokenKind.LParen).prefixParser = ParseGroup;
		pratt.Get(TokenKind.LBracket).prefixParser = ParseListExpr;
		pratt.Get(TokenKind.As, Constants.operatorPrecedence[TokenKind.As]).infixParser = ParseCastExpr;
		pratt.Get(TokenKind.Dot, Constants.operatorPrecedence[TokenKind.Dot]).infixParser = ParseMemberExpr;
		pratt.Get(TokenKind.LParen, Constants.operatorPrecedence[TokenKind.LParen]).infixParser = ParseCallExpr;
		pratt.Get(TokenKind.LParam, Constants.operatorPrecedence[TokenKind.LParam]).infixParser = ParseParamExpr;
		pratt.Get(TokenKind.LBracket, Constants.operatorPrecedence[TokenKind.LBracket]).infixParser = ParseIndexExpr;
		pratt.Get(TokenKind.Nullable, Constants.operatorPrecedence[TokenKind.Nullable]).infixParser = ParseNullableExpr;
		pratt.Get(TokenKind.NullableDot, Constants.operatorPrecedence[TokenKind.NullableDot]).infixParser = ParseMemberExpr;
	}
	
	private static IntExpr ParseIntExpr(ParserContext context)
	{
		int value;
		Token token = context.CurrentToken();
		try {
			if (token.text.StartsWith("0x")) {
				value = Convert.ToInt32(token.text.Substring(2), 16);
			} else if (token.text.StartsWith("0o")) {
				value = Convert.ToInt32(token.text.Substring(2), 8);
			} else if (token.text.StartsWith("0b")) {
				value = Convert.ToInt32(token.text.Substring(2), 2);
			} else {
				value = int.Parse(token.text);
			}
		} catch (Exception) {
			return null;
		}
		context.Next();
		return Wrap(context, token, new IntExpr { value = value });
	}
	
	private static FloatExpr ParseFloatExpr(ParserContext context)
	{
		float value;
		Token token = context.CurrentToken();
		try {
			value = float.Parse(token.text);
		} catch (Exception) {
			return null;
		}
		context.Next();
		return Wrap(context, token, new FloatExpr { value = value });
	}
	
	private static TypeExpr ParsePrimType(ParserContext context, Token token)
	{
		return Wrap(context, token, new TypeExpr {
			type = new PrimType { kind = Constants.tokenToPrim[token.kind] }
		});
	}
	
	private static UnaryExpr ParsePrefixExpr(ParserContext context, Token token, Expr value)
	{
		return Wrap(context, token, new UnaryExpr {
			op = token.kind.AsUnaryOp(),
			value = value
		});
	}
	
	private static BinaryExpr ParseBinaryExpr(ParserContext context, Expr left, Token token, Expr right)
	{
		return Wrap(context, token, new BinaryExpr {
			left = left,
			op = token.kind.AsBinaryOp(),
			right = right
		});
	}
	
	private static CastExpr ParseCastExpr(ParserContext context, Expr left)
	{
		// Create the node
		CastExpr node = Wrap(context, null, new CastExpr { value = left });
		context.Next();
		
		// Parse the target type
		node.target = pratt.Parse(context, Constants.operatorPrecedence[TokenKind.As]);
		if (node.target == null) {
			return null;
		}
		
		return node;
	}
	
	private static MemberExpr ParseMemberExpr(ParserContext context, Expr left)
	{
		// Create the node
		MemberExpr node = Wrap(context, null, new MemberExpr {
			obj = left,
			isSafeDereference = (context.CurrentToken().kind == TokenKind.NullableDot)
		});
		context.Next();
		
		// Parse the member identifier
		node.name = context.CurrentToken().text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		return node;
	}
	
	private static Expr ParseGroup(ParserContext context)
	{
		// A group is an expression wrapped in parentheses
		context.Next();
		Expr node = pratt.Parse(context);
		if (node == null || !context.Consume(TokenKind.RParen)) {
			return null;
		}
		return node;
	}
	
	private static ListExpr ParseListExpr(ParserContext context)
	{
		// Create the node
		ListExpr node = Wrap(context, null, new ListExpr { items = new List<Expr>() });
		context.Next();
		
		// Parse the item list
		bool first = true;
		while (!context.Consume(TokenKind.RBracket)) {
			if (first) {
				first = false;
			} else if (!context.Consume(TokenKind.Comma)) {
				return null;
			}
			Expr item = pratt.Parse(context);
			if (item == null) {
				return null;
			}
			node.items.Add(item);
		}
		
		return node;
	}
	
	private static CallExpr ParseCallExpr(ParserContext context, Expr left)
	{
		// Create the node
		CallExpr node = Wrap(context, null, new CallExpr { func = left, args = new List<Expr>() });
		context.Next();
		
		// Parse the argument list
		bool first = true;
		while (!context.Consume(TokenKind.RParen)) {
			if (first) {
				first = false;
			} else if (!context.Consume(TokenKind.Comma)) {
				return null;
			}
			Expr arg = pratt.Parse(context);
			if (arg == null) {
				return null;
			}
			node.args.Add(arg);
		}
		
		return node;
	}
	
	private static ParamExpr ParseParamExpr(ParserContext context, Expr left)
	{
		// Create the node
		ParamExpr node = Wrap(context, null, new ParamExpr { type = left, typeParams = new List<Expr>() });
		context.Next();
		
		// Parse the type parameter list with at least one parameter
		Expr param = pratt.Parse(context);
		if (param == null) {
			return null;
		}
		node.typeParams.Add(param);
		while (!context.Consume(TokenKind.RParam)) {
			if (!context.Consume(TokenKind.Comma)) {
				return null;
			}
			param = pratt.Parse(context);
			if (param == null) {
				return null;
			}
			node.typeParams.Add(param);
		}
		
		return node;
	}
	
	private static IndexExpr ParseIndexExpr(ParserContext context, Expr left)
	{
		// Create the node
		IndexExpr node = Wrap(context, null, new IndexExpr { obj = left });
		context.Next();
		
		// Parse the index
		if ((node.index = pratt.Parse(context)) == null || !context.Consume(TokenKind.RBracket)) {
			return null;
		}
		
		return node;
	}
	
	private static NullableExpr ParseNullableExpr(ParserContext context, Expr left)
	{
		// Create the node
		NullableExpr node = Wrap(context, null, new NullableExpr { value = left });
		context.Next();
		
		return node;
	}
	
	private static bool ParseEndOfStatement(ParserContext context)
	{
		return context.Consume(TokenKind.Semicolon) || context.Consume(TokenKind.Newline) ||
			context.Peek(TokenKind.RBrace) || context.Peek(TokenKind.EndOfFile);
	}
	
	private static Stmt ParseStmt(ParserContext context, Block block)
	{
		// Try to parse identifiable statements (ones that don't start with an expression)
		if (context.Peek(TokenKind.If)) {
			return ParseIfStmt(context);
		}
		if (context.Peek(TokenKind.Return)) {
			return ParseReturnStmt(context);
		}
		if (context.Peek(TokenKind.External)) {
			return ParseExternalStmt(context);
		}
		if (context.Peek(TokenKind.While)) {
			return ParseWhileStmt(context);
		}
		if (context.Peek(TokenKind.Class)) {
			return ParseClassDef(context);
		}
		
		// Check for modifiers now
		bool isStatic = context.Consume(TokenKind.Static);
		
		// If we don't know what it is yet, try an expression
		Token token = context.CurrentToken();
		Expr expr = pratt.Parse(context);
		if (expr == null) {
			return null;
		}
		
		// Check for end of statement (then it's a free expression)
		if (!isStatic && ParseEndOfStatement(context)) {
			return Wrap(context, token, new ExprStmt { value = expr });
		}
		token = context.CurrentToken();
		
		// Assume we have a definition (will start with a name)
		string name = token.text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		// Function definition
		if (context.Consume(TokenKind.LParen)) {
			FuncDef func = Wrap(context, token, new FuncDef {
				isStatic = isStatic,
				name = name,
				returnType = expr,
				argDefs = new List<VarDef>()
			});
			context.PushInfo().funcDef = func;
			if (isStatic) {
				context.Info().inStaticFunc = true;
			}
			
			// Parse arguments
			bool first = true;
			context.PushInfo().inFuncArgList = true;
			while (!context.Consume(TokenKind.RParen)) {
				if (first) {
					first = false;
				} else if (!context.Consume(TokenKind.Comma)) {
					context.PopInfo();
					context.PopInfo();
					return null;
				}
				VarDef arg = Wrap(context, null, new VarDef());
				if ((arg.type = pratt.Parse(context)) == null) {
					context.PopInfo();
					context.PopInfo();
					return null;
				}
				arg.name = context.CurrentToken().text;
				if (!context.Consume(TokenKind.Identifier)) {
					context.PopInfo();
					context.PopInfo();
					return null;
				}
				if (context.Consume(TokenKind.Assign) && (arg.value = pratt.Parse(context)) == null) {
					context.PopInfo();
					context.PopInfo();
					return null;
				}
				func.argDefs.Add(arg);
			}
			context.PopInfo();
			
			// Parse the block
			if (!ParseEndOfStatement(context) && ((func.block = ParseBlock(context)) == null || !ParseEndOfStatement(context))) {
				context.PopInfo();
				return null;
			}
			
			context.PopInfo();
			return func;
		}
		
		// Only possible option is a variable definition
		if (isStatic) {
			return null;
		}
		
		// Variable definition and initialization
		VarDef node = Wrap(context, token, new VarDef { name = name, type = expr });
		if (context.Consume(TokenKind.Assign) && (node.value = pratt.Parse(context)) == null) {
			return null;
		}
		
		// Check for additional variables and add them to the current block as
		// separate VarDef statements (returning just the last one)
		while (context.Consume(TokenKind.Comma)) {
			block.stmts.Add(node);
			node = Wrap(context, null, new VarDef { name = context.CurrentToken().text, type = expr });
			if (!context.Consume(TokenKind.Identifier)) {
				return null;
			}
			if (context.Consume(TokenKind.Assign) && (node.value = pratt.Parse(context)) == null) {
				return null;
			}
		}
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static IfStmt ParseIfStmt(ParserContext context)
	{
		// Create the node
		IfStmt node = Wrap(context, null, new IfStmt());
		context.Next();
		
		// Parse the if statement
		if ((node.test = pratt.Parse(context)) == null || (node.thenBlock = ParseBlock(context)) == null) {
			return null;
		}
		
		// A newline is optional after an if and before an else
		bool endOfStatement = ParseEndOfStatement(context);
		
		// Check for an else block (we don't have dangling else problems since we require braces around blocks)
		if (context.Consume(TokenKind.Else)) {
			// Special-case else if
			if (context.Peek(TokenKind.If)) {
				Stmt elseIf = ParseIfStmt(context);
				if (elseIf == null) {
					return null;
				}
				node.elseBlock = Wrap(context, null, new Block { stmts = new List<Stmt> { elseIf } });
				return node;
			}
			
			// Otherwise just parse an else block and forget the end of the statement because we've read more since then
			endOfStatement = false;
			if ((node.elseBlock = ParseBlock(context)) == null) {
				return null;
			}
		}
		
		// Check for end of statement
		if (!endOfStatement && !ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ReturnStmt ParseReturnStmt(ParserContext context)
	{
		// Create the node
		ReturnStmt node = Wrap(context, null, new ReturnStmt());
		context.Next();
		
		// First check for a void return
		if (ParseEndOfStatement(context)) {
			return node;
		}
		
		// Otherwise there must be an expression
		if ((node.value = pratt.Parse(context)) == null || !ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ExternalStmt ParseExternalStmt(ParserContext context)
	{
		// Create the node
		ExternalStmt node = Wrap(context, null, new ExternalStmt());
		context.Next();
		
		// Parse the block
		context.PushInfo().inExternal = true;
		if ((node.block = ParseBlock(context)) == null) {
			context.PopInfo();
			return null;
		}
		context.PopInfo();
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static WhileStmt ParseWhileStmt(ParserContext context)
	{
		// Create the node
		WhileStmt node = Wrap(context, null, new WhileStmt());
		context.Next();
		
		// Parse the condition
		if ((node.test = pratt.Parse(context)) == null) {
			return null;
		}
		
		// Parse the block
		if ((node.block = ParseBlock(context)) == null) {
			return null;
		}
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ClassDef ParseClassDef(ParserContext context)
	{
		// Create the node
		ClassDef node = Wrap(context, null, new ClassDef());
		context.Next();
		
		// Parse the name
		node.name = context.CurrentToken().text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		
		// Parse the block
		context.PushInfo().classDef = node;
		if ((node.block = ParseBlock(context)) == null) {
			context.PopInfo();
			return null;
		}
		context.PopInfo();
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static Block ParseBlock(ParserContext context)
	{
		// Create the node
		Block node = Wrap(context, null, new Block());
		
		// Read the opening brace, swallowing up to one newline on either side
		context.Consume(TokenKind.Newline);
		if (!context.Consume(TokenKind.LBrace)) {
			return null;
		}
		context.Consume(TokenKind.Newline);
		
		// Keep reading values until the closing brace
		node.stmts = new List<Stmt>();
		while (!context.Consume(TokenKind.RBrace)) {
			Stmt stmt = ParseStmt(context, node);
			if (stmt == null) {
				return null;
			}
			node.stmts.Add(stmt);
		}
		
		return node;
	}
	
	private static Module ParseModule(ParserContext context, string name)
	{
		// Create the node
		Module node = Wrap(context, null, new Module {
			name = name,
			block = Wrap(context, null, new Block { stmts = new List<Stmt>() })
		});
		
		// Keep reading statements until the end of the file
		context.Info().module = node;
		context.Consume(TokenKind.Newline);
		while (!context.Consume(TokenKind.EndOfFile)) {
			Stmt stmt = ParseStmt(context, node.block);
			if (stmt == null) {
				return null;
			}
			node.block.stmts.Add(stmt);
		}
		
		return node;
	}
	
	public static Module Parse(Log log, List<Token> tokens, string moduleName)
	{
		// Run the parser over the input
		ParserContext context = new ParserContext(tokens);
		Module node = ParseModule(context, moduleName);
		
		// Check that we parsed everything
		if (node != null) {
			return node;
		}
		
		// Assume the current token is the location of the error
		log.Error(context.CurrentToken().location, "unexpected " + context.CurrentToken().ErrorText());
		return null;
	}
}
