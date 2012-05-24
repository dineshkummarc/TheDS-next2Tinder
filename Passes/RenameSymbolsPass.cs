using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class RenameSymbolsPass : DefaultVisitor
{
	private static readonly Regex splitter = new Regex("[<>,.? ]+");
	private HashSet<string> reservedWords;
	private bool renameOverloads;
	
	public RenameSymbolsPass(HashSet<string> reservedWords, bool renameOverloads)
	{
		this.reservedWords = reservedWords;
		this.renameOverloads = renameOverloads;
	}
	
	private string MangleOverload(string name, List<Type> argTypes)
	{
		foreach (Type type in argTypes) {
			List<string> parts = new List<string>(splitter.Split(type.ToString()));
			name += parts.ConvertAll(x => x.Length == 0 ? "" : x.Substring(0, 1).ToUpper() + x.Substring(1)).Join();
		}
		return name;
	}
	
	private string Rename(string name, Scope scope)
	{
		while (reservedWords.Contains(name) || scope.Lookup(name, LookupKind.Any) != null) {
			name = "_" + name;
		}
		return name;
	}
	
	public override Null Visit(Block node)
	{
		foreach (KeyValuePair<string, Symbol> pair in node.scope.map.Items()) {
			// See if we have to rename
			string oldName = pair.Key;
			Symbol symbol = pair.Value;
			if (!reservedWords.Contains(oldName) && (!renameOverloads || symbol.kind != SymbolKind.OverloadedFunc)) {
				continue;
			}
			
			// If we need to rename, remove the symbol first so lookups will return useful info
			node.scope.map.Remove(oldName);
			symbol.finalName = Rename(oldName, node.scope);
			if (!renameOverloads || symbol.kind != SymbolKind.OverloadedFunc) {
				node.scope.map.Add(symbol.finalName, symbol);
			}
			
			// Rename all overloads
			if (symbol.kind == SymbolKind.OverloadedFunc) {
				foreach (Symbol overload in ((OverloadedFuncType)symbol.type).overloads) {
					if (renameOverloads) {
						overload.finalName = Rename(MangleOverload(overload.def.name, overload.type.ArgTypes()), node.scope);
						node.scope.map.Add(overload.finalName, overload);
					} else {
						overload.finalName = symbol.finalName;
					}
				}
			}
		}
		base.Visit(node);
		return null;
	}
}
