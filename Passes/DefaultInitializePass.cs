using System;

public class DefaultInitializePass : DefaultVisitor
{
	public override Null Visit(ExternalStmt node)
	{
		// Initialization isn't possible in external statements
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Don't initialize arguments
		node.block.Accept(this);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		if (node.value == null) {
			Type type = node.type.computedType.InstanceType();
			if (type.IsBool()) {
				node.value = new BoolExpr { value = false, computedType = type, location = node.location };
			} else if (type.IsInt()) {
				node.value = new IntExpr { value = 0, computedType = type, location = node.location };
			} else if (type.IsFloat()) {
				node.value = new FloatExpr { value = 0, computedType = type, location = node.location };
			} else if (type.IsString()) {
				node.value = new StringExpr { value = "", computedType = type, location = node.location };
			} else {
				node.value = new CastExpr {
					value = new NullExpr { computedType = new NullType(), location = node.location },
					target = new TypeExpr { type = type, computedType = new MetaType { instanceType = type } },
					computedType = type
				};
			}
		}
		return null;
	}
}
