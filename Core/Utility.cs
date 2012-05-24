using System;
using System.Collections;
using System.Collections.Generic;

public class Log
{
	public List<string> warnings = new List<string>();
	public List<string> errors = new List<string>();
	public bool disabled;
	
	public void Warning(Location location, string text)
	{
		if (!disabled) {
			warnings.Add(Location.Where(location) + ": warning: " + text);
		}
	}
	
	public void Error(Location location, string text)
	{
		if (!disabled) {
			errors.Add(Location.Where(location) + ": error: " + text);
		}
	}
}

public class OperatorInfo<T>
{
	public TokenKind kind;
	public int precedence;
	public T op;
}

public class OperatorList<T> : IEnumerable
{
	public List<OperatorInfo<T>> list = new List<OperatorInfo<T>>();
	public Dictionary<TokenKind, T> tokenToEnum = new Dictionary<TokenKind, T>();
	public Dictionary<T, TokenKind> enumToToken = new Dictionary<T, TokenKind>();
	
	public IEnumerator GetEnumerator()
	{
		return list.GetEnumerator();
	}
	
	public void Add(TokenKind kind, int precedence, T op)
	{
		list.Add(new OperatorInfo<T> { kind = kind, precedence = precedence, op = op });
		tokenToEnum[kind] = op;
		enumToToken[op] = kind;
	}
}

public static class Constants
{
	// Note: Operator precedence levels need spacing in between them so we can
	// parse right-associative operators using a Pratt parser. While we only
	// need the levels to be a multiple of 2 so we can add 1, making them a
	// multiple of 10 brings them closer visually to their original values.
	
	// Table of binary operators mapping TokenKind <=> BinaryOp
	public static readonly OperatorList<BinaryOp> binaryOperators = new OperatorList<BinaryOp> {
		{ TokenKind.Assign, 10, BinaryOp.Assign },
		
		{ TokenKind.NullableDefault, 20, BinaryOp.NullableDefault },
		
		{ TokenKind.And, 30, BinaryOp.And },
		{ TokenKind.Or, 30, BinaryOp.Or },
		
		{ TokenKind.Equal, 40, BinaryOp.Equal },
		{ TokenKind.NotEqual, 40, BinaryOp.NotEqual },
		
		{ TokenKind.LessThan, 50, BinaryOp.LessThan },
		{ TokenKind.GreaterThan, 50, BinaryOp.GreaterThan },
		{ TokenKind.LessThanEqual, 50, BinaryOp.LessThanEqual },
		{ TokenKind.GreaterThanEqual, 50, BinaryOp.GreaterThanEqual },
		
		{ TokenKind.LShift, 60, BinaryOp.LShift },
		{ TokenKind.RShift, 60, BinaryOp.RShift },
		{ TokenKind.BitAnd, 60, BinaryOp.BitAnd },
		{ TokenKind.BitOr, 60, BinaryOp.BitOr },
		{ TokenKind.BitXor, 60, BinaryOp.BitXor },
		
		{ TokenKind.Add, 70, BinaryOp.Add },
		{ TokenKind.Subtract, 70, BinaryOp.Subtract },
		
		{ TokenKind.Multiply, 80, BinaryOp.Multiply },
		{ TokenKind.Divide, 80, BinaryOp.Divide },
	};
	
	// Table of right-associative binary operators
	public static readonly List<TokenKind> rightAssociativeOperators = new List<TokenKind>() {
		TokenKind.Assign,
	};
	
	// Table of unary operators mapping TokenKind <=> UnaryOp
	public static readonly OperatorList<UnaryOp> unaryOperators = new OperatorList<UnaryOp> {
		{ TokenKind.Subtract, 100, UnaryOp.Negative },
		{ TokenKind.Not, 100, UnaryOp.Not },
	};
	
	// Operator precedence table for operators not in binaryOperators and unaryOperators
	public static readonly Dictionary<TokenKind, int> operatorPrecedence = new Dictionary<TokenKind, int> {
		{ TokenKind.As, 90 },
		{ TokenKind.Dot, 110 },
		{ TokenKind.LParen, 110 },
		{ TokenKind.LParam, 110 },
		{ TokenKind.LBracket, 110 },
		{ TokenKind.Nullable, 110 },
		{ TokenKind.NullableDot, 110 },
	};
	
	// Map all symbols, operators, and keywords to the equivalent TokenKind
	public static readonly Dictionary<string, TokenKind> stringToToken = new Dictionary<string, TokenKind> {
		{ "(", TokenKind.LParen },
		{ ")", TokenKind.RParen },
		{ "[", TokenKind.LBracket },
		{ "]", TokenKind.RBracket },
		{ "{", TokenKind.LBrace },
		{ "}", TokenKind.RBrace },
		{ ",", TokenKind.Comma },
		{ ";", TokenKind.Semicolon },
		{ ".", TokenKind.Dot },
		{ ":", TokenKind.Colon },
		{ "\\", TokenKind.Backslash },
		
		{ "+", TokenKind.Add },
		{ "-", TokenKind.Subtract },
		{ "*", TokenKind.Multiply },
		{ "/", TokenKind.Divide },
		{ "<<", TokenKind.LShift },
		{ ">>", TokenKind.RShift },
		{ "&", TokenKind.BitAnd },
		{ "|", TokenKind.BitOr },
		{ "^", TokenKind.BitXor },
		{ "==", TokenKind.Equal },
		{ "!=", TokenKind.NotEqual },
		{ "<=", TokenKind.LessThanEqual },
		{ ">=", TokenKind.GreaterThanEqual },
		{ "<", TokenKind.LessThan },
		{ ">", TokenKind.GreaterThan },
		{ "?.", TokenKind.NullableDot },
		{ "??", TokenKind.NullableDefault },
		{ "?", TokenKind.Nullable },
		{ "=", TokenKind.Assign },
		
		{ "if", TokenKind.If },
		{ "else", TokenKind.Else },
		{ "while", TokenKind.While },
		{ "class", TokenKind.Class },
		{ "return", TokenKind.Return },
		{ "and", TokenKind.And },
		{ "or", TokenKind.Or },
		{ "not", TokenKind.Not },
		{ "as", TokenKind.As },
		{ "external", TokenKind.External },
		{ "static", TokenKind.Static },
		{ "var", TokenKind.Var },
		{ "null", TokenKind.Null },
		{ "this", TokenKind.This },
		{ "true", TokenKind.True },
		{ "false", TokenKind.False },
		{ "void", TokenKind.Void },
		{ "bool", TokenKind.Bool },
		{ "int", TokenKind.Int },
		{ "float", TokenKind.Float },
		{ "string", TokenKind.String },
		{ "list", TokenKind.List },
		{ "function", TokenKind.Function },
	};
	
	// Map tokens for primitive types to the equivalent PrimKind
	public static readonly Dictionary<TokenKind, PrimKind> tokenToPrim = new Dictionary<TokenKind, PrimKind> {
		{ TokenKind.Bool, PrimKind.Bool },
		{ TokenKind.Int, PrimKind.Int },
		{ TokenKind.Float, PrimKind.Float },
		{ TokenKind.String, PrimKind.String },
	};
	
	// Inverse mappings
	public static readonly Dictionary<TokenKind, string> tokenToString = stringToToken.Inverse();
	public static readonly Dictionary<PrimKind, TokenKind> primToToken = tokenToPrim.Inverse();
}

public static class Utility
{
	public static string Quote(int c, char quote)
	{
		if (c == quote) {
			return "\\" + quote;
		}
		switch (c) {
			case '\t':
				return "\\t";
			case '\r':
				return "\\r";
			case '\n':
				return "\\n";
			case '\\':
				return "\\\\";
		}
		if (c >= 0x20 && c <= 0x7E) {
			return ((char)c).ToString();
		}
		if (c >= 0x00 && c <= 0xFF) {
			return "\\x" + string.Format("{0:X}", c).PadLeft(2, '0');
		}
		return "\\u" + string.Format("{0:X}", c).PadLeft(4, '0');
	}
	
	public static string ToQuotedString(this string text)
	{
		List<char> chars = new List<char>(text.ToCharArray());
		return "\"" + chars.ConvertAll(x => Quote(x, '"')).Join() + "\"";
	}
	
	public static string ToQuotedChar(this int c)
	{
		return "'" + Quote(c, '\'') + "'";
	}
	
	public static Dictionary<V, K> Inverse<K, V>(this Dictionary<K, V> dict)
	{
		Dictionary<V, K> inverse = new Dictionary<V, K>();
		foreach (KeyValuePair<K, V> pair in dict)
			inverse[pair.Value] = pair.Key;
		return inverse;
	}
	
	public static List<KeyValuePair<K, V>> Items<K, V>(this Dictionary<K, V> dict)
	{
		List<KeyValuePair<K, V>> pairs = new List<KeyValuePair<K, V>>();
		foreach (KeyValuePair<K, V> pair in dict)
			pairs.Add(pair);
		return pairs;
	}
	
	public static void AddRange<K, V>(this Dictionary<K, V> dict, Dictionary<K, V> other)
	{
		foreach (KeyValuePair<K, V> pair in other) {
			dict.Add(pair.Key, pair.Value);
		}
	}
	
	public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> other)
	{
		foreach (T t in other) {
			hashSet.Add(t);
		}
	}

	public static List<B> ConvertAll<A, B>(this HashSet<A> hashSet, Func<A, B> func)
	{
		List<B> result = new List<B>();
		foreach (A a in hashSet) {
			result.Add(func(a));
		}
		return result;
	}
	
	public static V GetOrNull<K, V>(this Dictionary<K, V> dict, K key) where V : class
	{
		V value;
		if (dict.TryGetValue(key, out value)) {
			return value;
		}
		return null;
	}
	
	public static V GetOrCreate<K, V>(this Dictionary<K, V> dict, K key) where V : new()
	{
		V value;
		if (!dict.TryGetValue(key, out value)) {
			value = dict[key] = new V();
		}
		return value;
	}
	
	public static V GetOrDefault<K, V>(this Dictionary<K, V> dict, K key, V defaultValue)
	{
		V value;
		if (dict.TryGetValue(key, out value)) {
			return value;
		}
		return defaultValue;
	}
	
	public static UnaryOp AsUnaryOp(this TokenKind kind)
	{
		return Constants.unaryOperators.tokenToEnum[kind];
	}
	
	public static BinaryOp AsBinaryOp(this TokenKind kind)
	{
		return Constants.binaryOperators.tokenToEnum[kind];
	}
	
	public static string AsString(this UnaryOp op)
	{
		return Constants.tokenToString[Constants.unaryOperators.enumToToken[op]];
	}
	
	public static string AsString(this BinaryOp op)
	{
		return Constants.tokenToString[Constants.binaryOperators.enumToToken[op]];
	}
	
	public static bool CanImplicitlyConvertTo(this Type from, Type to)
	{
		if (from.IsInt() && to.IsFloat()) {
			return true;
		}
		if (from is NullableType) {
			Type type = ((NullableType)from).type;
			if (to.EqualsType(type) || to.CanImplicitlyConvertTo(type)) {
				return true;
			}
		}
		if (to is NullableType) {
			Type type = ((NullableType)to).type;
			if (from is NullType || from.EqualsType(type) || from.CanImplicitlyConvertTo(type)) {
				return true;
			}
		}
		return false;
	}
	
	public static bool MatchesExactly(this List<Type> a, List<Type> b)
	{
		if (a.Count != b.Count) {
			return false;
		}
		for (int i = 0; i < a.Count; i++) {
			if (!a[i].EqualsType(b[i])) {
				return false;
			}
		}
		return true;
	}
	
	public static bool MatchesWithImplicitConversions(this List<Type> from, List<Type> to)
	{
		if (from.Count != to.Count) {
			return false;
		}
		for (int i = 0; i < from.Count; i++) {
			if (!from[i].EqualsType(to[i]) && !from[i].CanImplicitlyConvertTo(to[i])) {
				return false;
			}
		}
		return true;
	}
	
	public static string Join(this List<Type> argTypes)
	{
		return argTypes.ConvertAll(arg => arg.ToString()).Join(", ");
	}
	
	public static bool IsBool(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Bool;
	}
	
	public static bool IsInt(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Int;
	}
	
	public static bool IsFloat(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Float;
	}
	
	public static bool IsString(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.String;
	}
	
	public static bool IsNumeric(this Type type)
	{
		return type.IsInt() || type.IsFloat();
	}
	
	public static bool HasFreeParams(this Type type)
	{
		return (type is ListType && type.ItemType() == null) || (type is FuncType && type.ReturnType() == null);
	}
	
	public static bool IsCompleteType(this Type type)
	{
		return type is MetaType && !type.InstanceType().HasFreeParams();
	}

	public static bool IsInstantiatableType(this Type type)
	{
		if (!(type is MetaType)) {
			return false;
		}
		type = type.InstanceType();
		while (type is NullableType) {
			type = ((NullableType)type).type;
		}
		return (type is VoidType) ? false : !type.HasFreeParams();
	}
	
	public static Type ItemType(this Type type)
	{
		return ((ListType)type).itemType;
	}
	
	public static Type InstanceType(this Type type)
	{
		return ((MetaType)type).instanceType;
	}
	
	public static Type ReturnType(this Type type)
	{
		return ((FuncType)type).returnType;
	}
	
	public static List<Type> ArgTypes(this Type type)
	{
		return ((FuncType)type).argTypes;
	}
	
	public static NullableType AsNullableType(this Type type)
	{
		// Don't nest more than one level of nullable types
		if (type is NullableType) {
			return (NullableType)type;
		}
		return new NullableType { type = type };
	}
	
	public static string StripParens(this string text)
	{
		return text.StartsWith("(") && text.EndsWith(")") ? text.Substring(1, text.Length - 2) : text;
	}
	
	public static string Join(this List<string> list, string separator = "")
	{
		return string.Join(separator, list.ToArray());
	}

	public static string AsString(this IsNull isNull)
	{
		switch (isNull) {
			case IsNull.No: return "not null";
			case IsNull.Yes: return "null";
			case IsNull.Maybe: return "maybe null";
			case IsNull.Unknown: return "unknown";
		}
		return null;
	}
}
