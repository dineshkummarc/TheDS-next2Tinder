using System;

public class DefineSymbolsPass : DefaultVisitor
{
	private Log log;
	
	public DefineSymbolsPass(Log log)
	{
		this.log = log;
	}

	public override Null Visit(Module node)
	{
		// Make a module scope
		node.block.scope = new Scope(null, log, ScopeKind.Module);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		// Define the variable
		node.symbol = new Symbol {
			kind = SymbolKind.Variable,
			isStatic = false,
			def = node,
			type = new ErrorType()
		};
		scope.Define(node.symbol);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(ClassDef node)
	{
		// Define the class
		node.symbol = new Symbol {
			kind = SymbolKind.Class,
			isStatic = true,
			def = node,
			type = new MetaType { instanceType = new ClassType { def = node } }
		};
		scope.Define(node.symbol);
		
		// Make a class scope
		node.block.scope = new Scope(scope, log, ScopeKind.Class);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Define the function
		node.symbol = new Symbol {
			kind = SymbolKind.Func,
			isStatic = node.isStatic,
			def = node,
			type = new ErrorType()
		};
		scope.Define(node.symbol);
		
		// Visit the children differently than base.Visit(node) because we
		// want to define arguments in the body scope, not the parent scope.
		// Note that we don't visit the body, that's for ComputeTypesPass.
		node.returnType.Accept(this);
		if (node.block != null) {
			// Define arguments in the scope of the body
			node.block.scope = new Scope(scope, log, ScopeKind.Func);
			scope = node.block.scope;
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
		} else {
			// Define arguments in a temporary scope if no body is present
			scope = new Scope(scope, log, ScopeKind.Func);
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
		}
		
		return null;
	}
	
	public override Null Visit(ExternalStmt node)
	{
		// External statements don't have their own scope
		node.block.scope = scope;
		base.Visit(node);
		return null;
	}
}
