using System;
using System.Collections.Generic;

public class Location
{
	public string file;
	public int line;
	public int column;
	
	public static string Where(Location location)
	{
		if (location == null) {
			return "<unprintable location>";
		}
		return location.file + ":" + location.line + ":" + location.column;
	}
}

public enum TokenKind
{
	// Punctuation
	LParen,
	RParen,
	LBracket,
	RBracket,
	LBrace,
	RBrace,
	Comma,
	Semicolon,
	Dot,
	Colon,
	Backslash,
	
	// Operators
	Assign,
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
	NullableDot,
	NullableDefault,
	Nullable,
	
	// Keywords
	If,
	Else,
	While,
	Class,
	Return,
	And,
	Or,
	Not,
	As,
	External,
	Static,
	Var,
	Null,
	This,
	True,
	False,
	Void,
	Bool,
	Int,
	Float,
	String,
	List,
	Function,
	
	// Literals
	IntLit,
	CharLit,
	FloatLit,
	StringLit,
	
	// Other
	LParam,
	RParam,
	Identifier,
	Newline,
	EndOfFile,
	Error,
}

public class Token
{
	public TokenKind kind;
	public string text;
	public Location location;
	
	public string ErrorText()
	{
		// Special-case kinds that would look odd when printed quoted
		switch (kind) {
			case TokenKind.StringLit:
				return "string literal";
			case TokenKind.Newline:
				return "newline";
			case TokenKind.Backslash:
				return "backslash";
			case TokenKind.EndOfFile:
				return "end of file";
		}
		return '"' + text + '"';
	}
}

public class Tokenizer
{
	private static List<TokenKind> typeParamListTokenKinds = new List<TokenKind> {
		TokenKind.Identifier,
		TokenKind.Dot,
		TokenKind.Comma,
		TokenKind.LessThan,
		TokenKind.GreaterThan,
		TokenKind.Void,
		TokenKind.Bool,
		TokenKind.Int,
		TokenKind.Float,
		TokenKind.String,
		TokenKind.List,
		TokenKind.Function,
		TokenKind.RShift,
		TokenKind.Nullable,
	};
	private static Dictionary<TokenKind, TokenKind> oppositeBracket = new Dictionary<TokenKind, TokenKind> {
		{ TokenKind.RParen, TokenKind.LParen },
		{ TokenKind.RBracket, TokenKind.LBracket },
		{ TokenKind.RBrace, TokenKind.LBrace },
		{ TokenKind.GreaterThan, TokenKind.LessThan },
		{ TokenKind.RShift, TokenKind.LessThan },
	};
	private static List<TokenKind> leftBrackets = new List<TokenKind>(oppositeBracket.Values);
	private static List<TokenKind> rightBrackets = new List<TokenKind>(oppositeBracket.Keys);
	
	private static bool IsNumber(char c)
	{
		return c >= '0' && c <= '9';
	}
	
	private static bool IsLetter(char c)
	{
		return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
	}
	
	private static bool IsSpace(char c)
	{
		return c == ' ' || c == '\r' || c == '\n' || c == '\t';
	}
	
	private static List<Token> RawTokenize(Log log, string file, string text)
	{
		List<Token> tokens = new List<Token>();
		int i = 0, line = 1, lineStart = 0;

		while (i < text.Length) {
			// Create an error token by default
			Token token = new Token {
				kind = TokenKind.Error,
				text = "",
				location = new Location { file = file, line = line, column = i - lineStart + 1 }
			};
			int start = i;
			char c = text[i++];
			
			if (IsNumber(c)) {
				// Parse int literal
				token.kind = TokenKind.IntLit;
				while (i < text.Length && (IsNumber(text[i]) || IsLetter(text[i]))) {
					i++;
				}
				if (i + 1 < text.Length && text[i] == '.' && IsNumber(text[i + 1])) {
					// Parse float literal
					token.kind = TokenKind.FloatLit;
					i += 2;
					while (i < text.Length && IsNumber(text[i])) {
						i++;
					}
				}
			} else if (IsLetter(c)) {
				// Parse identifier
				while (i < text.Length && (IsLetter(text[i]) || IsNumber(text[i]))) {
					i++;
				}
				token.kind = TokenKind.Identifier;
			} else if (IsSpace(c)) {
				// Parse whitespace
				i--;
				while (i < text.Length && IsSpace(text[i])) {
					if (text[i] == '\n') {
						token.kind = TokenKind.Newline;
						line++;
						lineStart = i + 1;
					}
					i++;
				}
				if (token.kind == TokenKind.Error) {
					continue;
				}
			} else if (c == '"' || c == '\'') {
				// Parse string literal
				while (i < text.Length) {
					if (text[i] == c) {
						i++;
						token.kind = (c == '"') ? TokenKind.StringLit : TokenKind.CharLit;
						break;
					} else if (text[i] == '\\' && i + 1 < text.Length) {
						i++;
						char escape = text[i++];
						switch (escape) {
							case 't':
								token.text += '\t';
								break;
							case 'r':
								token.text += '\r';
								break;
							case 'n':
								token.text += '\n';
								break;
							case '\\':
								token.text += '\\';
								break;
							default:
								if (escape != c) {
									log.Warning(token.location, "string contains invalid escape sequence '\\" + escape + "'");
									token.text += "\\" + escape;
								} else {
									token.text += escape;
								}
								break;
						}
					} else {
						token.text += text[i++];
					}
				}
				if (token.kind == TokenKind.Error) {
					log.Error(token.location, "unterminated " + (c == '\'' ? "character" : "string") + " literal");
					break;
				}
				if (token.kind == TokenKind.CharLit && token.text.Length != 1) {
					log.Error(token.location, "invalid character literal");
					break;
				}
				tokens.Add(token);
				continue;
			} else if (c == '/' && i < text.Length && text[i] == '/') {
				// Check for single line comments
				while (i < text.Length && text[i] != '\n') {
					i++;
				}
				continue;
			} else if (c == '/' && i < text.Length && text[i] == '+') {
				// Check for multi-line comments (they nest, unlike C-style comments)
				int count = 1;
				i++;
				while (i + 1 < text.Length) {
					if (text[i] == '/' && text[i + 1] == '+') {
						count++;
						i += 2;
					} else if (text[i] == '+' && text[i + 1] == '/') {
						i += 2;
						if (--count == 0) {
							break;
						}
					} else if (text[i] == '\n') {
						line++;
						lineStart = ++i;
					} else {
						i++;
					}
				}
				if (count > 0) {
					log.Error(token.location, "unterminated multi-line comment");
					break;
				}
				continue;
			} else if (c == '\\') {
				token.kind = TokenKind.Backslash;
			} else {
				// Check for symbols
				i--;
				foreach (KeyValuePair<string, TokenKind> pair in Constants.stringToToken) {
					if (i + pair.Key.Length <= text.Length && text.IndexOf(pair.Key, i, pair.Key.Length) == i) {
						i += pair.Key.Length;
						token.kind = pair.Value;
						break;
					}
				}
				if (token.kind == TokenKind.Error) {
					log.Error(token.location, "unexpected '" + c + "'");
					i++;
				}
			}
			
			// Set the text based on how far we've parsed
			token.text = text.Substring(start, i - start);
			tokens.Add(token);
		}
		
		// Every token stream ends in a newline and an EOF token
		Location end = new Location { file = file, line = line, column = i - lineStart + 1 };
		if (tokens.Count == 0 || tokens[tokens.Count - 1].kind != TokenKind.Newline) {
			tokens.Add(new Token {
				kind = TokenKind.Newline,
				text = "",
				location = end
			});
		}
		tokens.Add(new Token {
			kind = TokenKind.EndOfFile,
			text = "",
			location = end
		});
		
		return tokens;
	}
	
	public static List<Token> Tokenize(Log log, string file, string text)
	{
		List<Token> tokens = RawTokenize(log, file, text);
		Stack<Token> stack = new Stack<Token>();
		
		// Type parameters are able to be specified via angle brackets because
		// of tokenizer tricks below. Some of the tricky cases are:
		//
		//     (x < y) > z
		//     x < (y > z)
		//     x < y and y > z
		//     x < y<z>()
		//
		// These are handled by keeping track of nested brackets for (), [], {},
		// and <> using a stack, and converting matching <> to type parameter
		// start and end tokens. To handle the less-than operator, if anything
		// other than an identifier, a dot, a comma, or an angle bracket is
		// encountered, all less-than tokens on the top of the stack are discarded.
		
		for (int i = 0; i < tokens.Count; i++) {
			Token token = tokens[i];
			
			if (token.kind == TokenKind.Identifier && Constants.stringToToken.ContainsKey(token.text)) {
				// Convert identifiers to keywords
				token.kind = Constants.stringToToken[token.text];
			}
			
			if (!typeParamListTokenKinds.Contains(token.kind)) {
				// Break out of all type parameter lists
				while (stack.Count > 0 && stack.Peek().kind == TokenKind.LessThan) {
					stack.Pop();
				}
			}
			
			if (leftBrackets.Contains(token.kind)) {
				// Keep track of the current bracket nesting using a stack
				stack.Push(token);
			} else if (stack.Count > 0 && rightBrackets.Contains(token.kind)) {
				// Keep track of the current bracket nesting
				Token top = stack.Pop();
				if (top.kind == TokenKind.LessThan) {
					// Steal a greater-than symbol from right shift tokens
					if (token.kind == TokenKind.RShift) {
						tokens.Insert(i + 1, new Token {
							kind = TokenKind.GreaterThan,
							text = ">",
							location = new Location {
								file = file,
								line = token.location.line,
								column = token.location.column + 1
							}
						});
						token.text = ">";
					}
					
					// Convert angle brackets to type parameter tokens
					top.kind = TokenKind.LParam;
					token.kind = TokenKind.RParam;
				}
			} else if (token.kind == TokenKind.Newline && stack.Count > 0 && stack.Peek().kind != TokenKind.LBrace) {
				// Remove newlines inside "()" and "[]" but not "{}" (or "<>", but those are handled already)
				tokens.RemoveAt(i--);
			} else if (token.kind == TokenKind.Newline && i + 1 < tokens.Count && tokens[i + 1].kind == TokenKind.Newline) {
				// Remove consecutive newlines (generated around comments)
				token.text += tokens[i + 1].text;
				tokens.RemoveAt(i + 1);
				i--;
			} else if (token.kind == TokenKind.Backslash && i + 1 < tokens.Count && tokens[i + 1].kind == TokenKind.Newline) {
				// Remove escaped newlines
				tokens.RemoveRange(i--, 2);
			}
		}
		
		return tokens;
	}
}
