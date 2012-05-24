using System;

// Compute the type of all expressions that are expected to contain types, then
// use these to set symbol types. This way the types of all symbols will be
// known when we compute the types of the other expressions in a later pass.
public class ComputeSymbolTypesPass : DefaultVisitor
{
	private Log log;
	private ComputeTypesPass helper;

	public ComputeSymbolTypesPass(Log log)
	{
		this.log = log;
		helper = new ComputeTypesPass(log);
	}
	
	public Type GetInstanceType(Expr node, bool isReturnType)
	{
		// Special-case void types for return types
		if (isReturnType && node is TypeExpr && ((TypeExpr)node).type is VoidType) {
			return ((TypeExpr)node).type;
		}
		helper.scope = scope;
		node.Accept(helper);
		if (node.computedType.IsCompleteType()) {
			return node.computedType.InstanceType();
		}
		log.ErrorNotUseableType(node.location, node.computedType);
		return new ErrorType();
	}
	
	public override Null Visit(VarDef node)
	{
		base.Visit(node);
		node.symbol.type = GetInstanceType(node.type, false);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Don't visit the body, that's for ComputeTypesPass
		node.returnType.Accept(this);
		foreach (VarDef argDef in node.argDefs) {
			argDef.Accept(this);
		}

		// Construct a function type from the parsed return and argument types
		node.symbol.type = new FuncType {
			returnType = GetInstanceType(node.returnType, true),
			argTypes = node.argDefs.ConvertAll(arg => GetInstanceType(arg.type, false))
		};

		return null;
	}
}
