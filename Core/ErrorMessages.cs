using System;
using System.Collections.Generic;

public static class ErrorMessages
{
	private static string WrapType(Type type)
	{
		if (type is MetaType) {
			return "type \"" + type.InstanceType() + "\"";
		}
		return "value of type \"" + type + "\"";
	}
	
	public static void ErrorRedefinition(this Log log, Location location, string name)
	{
		log.Error(location, "redefinition of " + name + " in the same scope");
	}
	
	public static void ErrorStmtNotAllowed(this Log log, Location location, string statement, string place)
	{
		log.Error(location, statement + " is not allowed " + place);
	}
	
	public static void ErrorDefaultArgNotAllowed(this Log log, Location location)
	{
		log.Error(location, "functions cannot have default arguments");
	}
	
	public static void ErrorFunctionBody(this Log log, Location location, bool inExternal)
	{
		if (inExternal) {
			log.Error(location, "functions inside external blocks cannot have implementations");
		} else {
			log.Error(location, "functions outside external blocks must have implementations");
		}
	}
	
	public static void ErrorUndefinedSymbol(this Log log, Location location, string name)
	{
		log.Error(location, "reference to undefined symbol \"" + name + "\"");
	}
	
	public static void ErrorNotUseableType(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		log.Error(location, WrapType(type) + " is not a " + (type is MetaType ? "useable type" : "type"));
	}
	
	public static void ErrorBadNullableType(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		if (type is MetaType && type.InstanceType() is NullableType) {
			log.Error(location, WrapType(type) + " is already nullable");
		} else {
			log.Error(location, WrapType(type) + " cannot be nullable");
		}
	}
	
	public static void ErrorTypeMismatch(this Log log, Location location, Type expected, Type found)
	{
		if (expected is ErrorType || found is ErrorType) {
			return;
		}
		log.Error(location, "cannot implicitly convert " + WrapType(found) + " to " + WrapType(expected));
	}
	
	public static void ErrorUnaryOpNotFound(this Log log, UnaryExpr node)
	{
		if (node.value.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "no match for operator " + node.op.AsString() + " that takes arguments \"(" +
			node.value.computedType + ")\"");
	}
	
	public static void ErrorBinaryOpNotFound(this Log log, BinaryExpr node)
	{
		if (node.left.computedType is ErrorType || node.right.computedType is ErrorType) {
			return;
		}
		if (node.op == BinaryOp.Assign) {
			log.Error(node.location, "cannot assign " + WrapType(node.right.computedType) + " to " + WrapType(node.left.computedType));
		} else {
			log.Error(node.location, "no match for operator " + node.op.AsString() + " that takes arguments \"(" +
				node.left.computedType + ", " + node.right.computedType + ")\"");
		}
	}
	
	public static void ErrorInvalidCast(this Log log, Location location, Type from, Type to)
	{
		if (from is ErrorType || to is ErrorType) {
			return;
		}
		log.Error(location, "cannot cast value of type \"" + from + "\" to \"" + to + "\"");
	}
	
	public static void ErrorBadSaveDereference(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		log.Error(location, "cannot apply safe dereference operator \"?.\" to " + WrapType(type));
	}
	
	public static void ErrorBadMemberAccess(this Log log, MemberExpr node)
	{
		if (node.obj.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "cannot access member \"" + node.name + "\" on " + WrapType(node.obj.computedType));
	}

	public static void ErrorCallNotFound(this Log log, Location location, Type funcType, List<Type> argTypes)
	{
		if (funcType is ErrorType || argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "cannot call " + WrapType(funcType) + " with arguments \"(" + argTypes.Join() + ")\"");
	}
	
	public static void ErrorMultipleOverloadsFound(this Log log, Location location, List<Type> argTypes)
	{
		if (argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "multiple ambiguous overloads that match arguments \"(" + argTypes.Join() + ")\"");
	}
	
	public static void ErrorBadThis(this Log log, Location location)
	{
		log.Error(location, "\"this\" used outside a member function");
	}
	
	public static void ErrorVoidReturn(this Log log, Location location, bool shouldBeVoid)
	{
		if (shouldBeVoid) {
			log.Error(location, "returning value from function returning void");
		} else {
			log.Error(location, "missing return value in non-void function");
		}
	}
	
	public static void ErrorNotAllPathsReturnValue(this Log log, Location location)
	{
		log.Error(location, "not all control paths return a value");
	}
	
	public static void ErrorUseBeforeDefinition(this Log log, Location location, string name)
	{
		log.Error(location, "use of variable \"" + name + "\" before its definition");
	}
	
	public static void ErrorOverloadChangedModifier(this Log log, Location location, string modifier)
	{
		log.Error(location, "overload has different " + modifier + " modifier than previous overload");
	}
	
	public static void ErrorNoOverloadContext(this Log log, Location location)
	{
		log.Error(location, "cannot resolve overloaded function without context");
	}
	
	public static void ErrorNoListContext(this Log log, Location location)
	{
		log.Error(location, "cannot resolve type of list literal without context");
	}
	
	public static void ErrorMetaTypeExpr(this Log log, Location location)
	{
		log.Error(location, "free expression evaluates to type description");
	}
	
	public static void ErrorBadTypeParamCount(this Log log, Location location, int expected, int found, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		if (expected == 0) {
			log.Error(location, "the type \"" + type + "\" does not have free type parameters");
		} else if (found == 0) {
			log.Error(location, "the type \"" + type + "\" requires type parameters");
		} else {
			log.Error(location, "expected " + expected + " type parameters but got " + found);
		}
	}
	
	public static void ErrorBadKeyword(this Log log, Location location, string keyword)
	{
		log.Error(location, "\"" + keyword + "\" is not allowed here");
	}

	public static void WarningDeadCode(this Log log, Location location)
	{
		log.Warning(location, "dead code");
	}
	
	public static void WarningNullDereference(this Log log, Location location, string name)
	{
		log.Warning(location, "dereference of definitely null value" + (name != null ? " \"" + name + "\"" : ""));
	}
	
	public static void WarningNullableDereference(this Log log, Location location, string name)
	{
		log.Warning(location, "dereference of possibly null value" + (name != null ? " \"" + name + "\"" : ""));
	}
}
