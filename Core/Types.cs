using System;
using System.Collections.Generic;

////////////////////////////////////////////////////////////////////////////////
// Types
////////////////////////////////////////////////////////////////////////////////

public abstract class Type
{
	public abstract bool EqualsType(Type other);
}

public class VoidType : Type
{
	public override bool EqualsType(Type other)
	{
		return other is VoidType;
	}
	
	public override string ToString()
	{
		return "void";
	}
}

public enum PrimKind
{
	Bool,
	Int,
	Float,
	String,
}

public class PrimType : Type
{
	public PrimKind kind;
	
	public override bool EqualsType(Type other)
	{
		return other is PrimType && kind == ((PrimType)other).kind;
	}
	
	public override string ToString()
	{
		return Constants.tokenToString[Constants.primToToken[kind]];
	}
}

public class ListType : Type
{
	public Type itemType;
	
	public override bool EqualsType(Type other)
	{
		return other is ListType && (itemType == other.ItemType() || itemType.EqualsType(other.ItemType()));
	}
	
	public override string ToString()
	{
		return "list" + (itemType == null ? "" : "<" + itemType + ">");
	}
}

public class FuncType : Type
{
	public Type returnType;
	public List<Type> argTypes;
	
	public override bool EqualsType(Type other)
	{
		return other is FuncType && returnType.EqualsType(other.ReturnType()) && argTypes.MatchesExactly(other.ArgTypes());
	}
	
	public override string ToString()
	{
		if (returnType == null) {
			return "function";
		}
		List<Type> types = new List<Type> { returnType };
		types.AddRange(argTypes);
		return "function<" + types.Join() + ">";
	}
}

public class ClassType : Type
{
	public ClassDef def;
	
	public override bool EqualsType(Type other)
	{
		return other is ClassType && def == ((ClassType)other).def;
	}
	
	public override string ToString()
	{
		return def.name;
	}
}

public class MetaType : Type
{
	public Type instanceType;
	
	public override bool EqualsType(Type other)
	{
		return other is MetaType && instanceType.EqualsType(other.InstanceType());
	}
	
	public override string ToString()
	{
		return "<type " + instanceType + ">";
	}
}

public class OverloadedFuncType : Type
{
	public List<Symbol> overloads;
	
	public override bool EqualsType(Type other)
	{
		return false;
	}
	
	public override string ToString()
	{
		return "<overloaded function>";
	}
}

public class NullType : Type
{
	public override bool EqualsType(Type other)
	{
		return other is NullType;
	}
	
	public override string ToString()
	{
		return "null";
	}
}

public class ErrorType : Type
{
	public override bool EqualsType(Type other)
	{
		return false;
	}
	
	public override string ToString()
	{
		return "<error>";
	}
}

public class NullableType : Type
{
	public Type type;
	
	public override bool EqualsType(Type other)
	{
		return other is NullableType && type.EqualsType(((NullableType)other).type);
	}
	
	public override string ToString()
	{
		return type + "?";
	}
}

////////////////////////////////////////////////////////////////////////////////
// Symbols
////////////////////////////////////////////////////////////////////////////////

public enum SymbolKind
{
	Func,
	Class,
	Variable,
	OverloadedFunc,
}

public class Symbol
{
	public SymbolKind kind;
	public bool isStatic; // Copied from def if present
	public Def def; // Might be null for generated symbols
	public Type type; // Might be null until after types are assigned to symbols
	public string finalName; // Starts off as def.name but may be renamed (USE THIS ONE FOR OUTPUT)
}

////////////////////////////////////////////////////////////////////////////////
// Scope
////////////////////////////////////////////////////////////////////////////////

public enum ScopeKind
{
	Func,
	Class,
	Module,
	Local,
}

public enum LookupKind
{
	Any,
	Normal,
	StaticMember,
	InstanceMember,
}

public class Scope
{
	public Log log;
	public Scope parent;
	public ScopeKind kind;
	public Dictionary<string, Symbol> map = new Dictionary<string, Symbol>();
	
	public Scope(Scope parent, Log log, ScopeKind kind)
	{
		this.parent = parent;
		this.kind = kind;
		this.log = log;
	}
	
	public void Define(Symbol symbol)
	{
		Symbol existing;
		symbol.finalName = symbol.def.name;
		if (!map.TryGetValue(symbol.def.name, out existing)) {
			// Insert a new symbol
			map.Add(symbol.def.name, symbol);
		} else if (symbol.kind == SymbolKind.Func && (existing.kind == SymbolKind.Func || existing.kind == SymbolKind.OverloadedFunc)) {
			// Don't let modifiers change when overloading
			if (existing.isStatic != symbol.isStatic) {
				log.ErrorOverloadChangedModifier(symbol.def.location, "static");
			}
			
			// Add to the overload, creating it if needed
			if (existing.kind == SymbolKind.Func) {
				map[symbol.def.name] = new Symbol {
					kind = SymbolKind.OverloadedFunc,
					isStatic = existing.isStatic,
					def = null, // On purpose
					type = new OverloadedFuncType { overloads = new List<Symbol> { existing, symbol } },
					finalName = symbol.def.name
				};
			} else {
				((OverloadedFuncType)existing.type).overloads.Add(symbol);
			}
		} else {
			// All other cases are errors
			log.ErrorRedefinition(symbol.def.location, symbol.def.name);
		}
	}
	
	public Symbol Lookup(string name, LookupKind lookupKind)
	{
		Symbol symbol;
		switch (lookupKind) {
			case LookupKind.Any:
				if (map.TryGetValue(name, out symbol)) {
					return symbol;
				}
				if (parent != null) {
					return parent.Lookup(name, LookupKind.Any);
				}
				break;
			
			case LookupKind.Normal:
				if (kind != ScopeKind.Class && map.TryGetValue(name, out symbol)) {
					return symbol;
				}
				if (parent != null) {
					return parent.Lookup(name, LookupKind.Normal);
				}
				break;
				
			case LookupKind.InstanceMember:
			case LookupKind.StaticMember:
				if (kind == ScopeKind.Class && map.TryGetValue(name, out symbol) && symbol.isStatic == (lookupKind == LookupKind.StaticMember)) {
					return symbol;
				}
				break;
		}
		return null;
	}
}
