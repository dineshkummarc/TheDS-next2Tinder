using System;

public static class Compiler
{
	public static bool Compile(Log log, Module module)
	{
		Visitor<Null>[] passes = new Visitor<Null>[] {
			new StructuralCheckPass(log),
			new DefineSymbolsPass(log),
			new ComputeSymbolTypesPass(log),
			new ComputeTypesPass(log),
			new DefaultInitializePass(),
			new FlowValidationPass(log),
		};
		foreach (Visitor<Null> pass in passes) {
			module.Accept(pass);
			if (log.errors.Count > 0) {
				return false;
			}
		}
		return true;
	}
}
