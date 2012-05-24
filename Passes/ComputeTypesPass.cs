using System;
using System.Collections.Generic;

public class ComputeTypesPass : DefaultVisitor
{
	public class Context
	{
		public List<Type> argTypes;
		public Type targetType;
	}
	
	private Log log;
	public Context context;
	
	public ComputeTypesPass(Log log)
	{
		this.log = log;
	}
	
	public override Null Visit(Block node)
	{
		// Only make a new scope if our parent node didn't make one already
		if (node.scope == null) {
			node.scope = new Scope(scope, log, ScopeKind.Local);
		}

		base.Visit(node);
		return null;
	}

	public override Null Visit(TypeExpr node)
	{
		context = null;
		node.computedType = new MetaType { instanceType = node.type };
		return null;
	}
	
	public override Null Visit(VarExpr node)
	{
		context = null;
		log.ErrorBadKeyword(node.location, "var");
		node.computedType = new ErrorType();
		return null;
	}
	
	public override Null Visit(NullExpr node)
	{
		context = null;
		node.computedType = new NullType();
		return null;
	}

	public override Null Visit(ThisExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		if (node.info.classDef != null && node.info.funcDef != null && !node.info.inStaticFunc) {
			node.computedType = new ClassType { def = node.info.classDef };
		} else {
			log.ErrorBadThis(node.location);
		}
		return null;
	}
	
	public override Null Visit(BoolExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Bool };
		return null;
	}
	
	public override Null Visit(IntExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Int };
		return null;
	}

	public override Null Visit(FloatExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Float };
		return null;
	}

	public override Null Visit(StringExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.String };
		return null;
	}
	
	public override Null Visit(IdentExpr node)
	{
		List<Type> argTypes = (context == null ? null : context.argTypes);
		
		// Perform the symbol lookup
		context = null;
		node.computedType = new ErrorType();
		node.symbol = scope.Lookup(node.name, LookupKind.Normal);
		if (node.symbol != null) {
			node.computedType = node.symbol.type;
		} else {
			log.ErrorUndefinedSymbol(node.location, node.name);
		}
		
		// Perform overload resolution using information we were provided
		ResolveOverloads(node, argTypes);
		
		return null;
	}
	
	public override Null Visit(ListExpr node)
	{
		node.computedType = new ErrorType();
		
		// Make sure we know what type the list items are supposed to be
		Type targetType = (context == null ? null : context.targetType);
		if (targetType == null) {
			log.ErrorNoListContext(node.location);
			return null;
		}
		if (!(targetType is ListType)) {
			log.ErrorTypeMismatch(node.location, targetType, new ListType());
			return null;
		}
		Type itemType = targetType.ItemType();
		
		// Make sure all items can be converted to that type
		Context itemTypeContext = new Context { targetType = itemType };
		for (int i = 0; i < node.items.Count; i++) {
			Expr item = node.items[i];
			context = itemTypeContext;
			item.Accept(this);
			if (!item.computedType.EqualsType(itemType)) {
				if (item.computedType.CanImplicitlyConvertTo(itemType)) {
					node.items[i] = InsertCast(item, itemType);
				} else {
					log.ErrorTypeMismatch(node.location, itemType, item.computedType);
				}
			}
		}
		
		node.computedType = new ListType { itemType = itemType };
		return null;
	}
	
	public override Null Visit(UnaryExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		switch (node.op) {
			case UnaryOp.Negative:
				if (node.value.computedType.IsNumeric()) {
					node.computedType = node.value.computedType;
				} else {
					log.ErrorUnaryOpNotFound(node);
				}
				break;
			case UnaryOp.Not:
				if (node.value.computedType.IsBool()) {
					node.computedType = node.value.computedType;
				} else {
					log.ErrorUnaryOpNotFound(node);
				}
				break;
		}
		
		return null;
	}
	
	public override Null Visit(BinaryExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		if (!SetUpBinaryOp(node)) {
			log.ErrorBinaryOpNotFound(node);
		}
		return null;
	}

	public override Null Visit(CallExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		
		// Context is tricky. For overloaded functions, we want to handle
		// arguments first and use them to provide context for overload
		// resolution. For normal functions, we want to handle the function
		// first and use its type to provide context for the arguments.
		// The problem is that we can't know which one to visit without visiting
		// one of them first. To handle this, we temporarily disable logging and
		// check if the function type is OverloadedFuncType or not.
		log.disabled = true;
		node.func.Accept(this);
		log.disabled = false;
		bool isOverload = node.func.computedType is OverloadedFuncType;
		
		if (isOverload) {
			// Visit the arguments first to get context for resolving overloads
			VisitAll(node.args);
			
			// Visit the function last and provide the overload resolving context
			context = new Context { argTypes = node.args.ConvertAll(arg => arg.computedType) };
			node.func.Accept(this);
		} else {
			// Visit the function again, this time with logging
			node.func.Accept(this);
			
			// Visit the arguments and provide context with the argument type
			FuncType funcType = node.func.computedType is FuncType ? (FuncType)node.func.computedType : null;
			for (int i = 0; i < node.args.Count; i++) {
				if (funcType != null && i < funcType.argTypes.Count) {
					context = new Context { targetType = funcType.argTypes[i] };
				}
				node.args[i].Accept(this);
			}
		}
		
		// Cache information for checking
		List<Type> argTypes = node.args.ConvertAll(arg => arg.computedType);
		Type type = node.func.computedType;
		
		// Check for constructors
		if (type.IsInstantiatableType() && argTypes.Count == 0 && type.InstanceType() is ClassType) {
			node.computedType = type.InstanceType();
			node.isCtor = true;
			return null;
		}
		
		// Call the function if there is one, inserting implicit casts as appropriate
		if (type is FuncType && argTypes.MatchesWithImplicitConversions(type.ArgTypes())) {
			FuncType funcType = (FuncType)type;
			for (int i = 0; i < funcType.argTypes.Count; i++) {
				if (!node.args[i].computedType.EqualsType(funcType.argTypes[i])) {
					node.args[i] = InsertCast(node.args[i], funcType.argTypes[i]);
				}
			}
			node.computedType = funcType.returnType;
		} else {
			log.ErrorCallNotFound(node.location, type, argTypes);
		}
		
		return null;
	}
	
	public override Null Visit(ParamExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Check type parameters first
		foreach (Expr expr in node.typeParams) {
			if (!expr.computedType.IsCompleteType()) {
				log.ErrorNotUseableType(expr.location, expr.computedType);
				return null;
			}
		}
		
		// Check type next, using type parameters to validate
		if (!(node.type.computedType is MetaType)) {
			log.ErrorNotUseableType(node.type.location, node.type.computedType);
			return null;
		}
		Type type = node.type.computedType.InstanceType();
		int paramCountFound = node.typeParams.Count;
		node.computedType = new ErrorType();
		if (type is ListType) {
			int paramCountExpected = (type.ItemType() == null) ? 1 : 0;
			if (paramCountFound != paramCountExpected) {
				log.ErrorBadTypeParamCount(node.location, paramCountExpected, paramCountFound, type);
			} else {
				node.computedType = new MetaType {
					instanceType = new ListType { itemType = node.typeParams[0].computedType.InstanceType() }
				};
			}
		} else if (type is FuncType) {
			if (type.ReturnType() != null) {
				log.ErrorBadTypeParamCount(node.location, 0, paramCountFound, type);
			} else {
				node.computedType = new MetaType {
					instanceType = new FuncType {
						returnType = node.typeParams[0].computedType.InstanceType(),
						argTypes = node.typeParams.GetRange(1, node.typeParams.Count - 1).ConvertAll(x => x.computedType.InstanceType())
					}
				};
			}
		} else {
			log.ErrorBadTypeParamCount(node.location, 0, paramCountFound, type);
		}
		
		return null;
	}
	
	public override Null Visit(CastExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		node.target.Accept(this);
		
		// Check that the cast is valid
		if (!node.target.computedType.IsInstantiatableType()) {
			log.ErrorNotUseableType(node.location, node.target.computedType);
		} else {
			Type targetType = node.target.computedType.InstanceType();
			context = new Context { targetType = targetType };
			node.value.Accept(this);
			if (!IsValidCast(node.value.computedType, targetType)) {
				log.ErrorInvalidCast(node.value.location, node.value.computedType, targetType);
			} else {
				node.computedType = targetType;
			}
		}
		
		return null;
	}
	
	public override Null Visit(MemberExpr node)
	{
		List<Type> argTypes = (context == null ? null : context.argTypes);
		
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Decide whether to do a static or instance symbol lookup
		LookupKind kind = LookupKind.InstanceMember;
		Type type = node.obj.computedType;
		if (type is MetaType) {
			type = type.InstanceType();
			kind = LookupKind.StaticMember;
		}
		
		// Nullable types automatically convert to their wrapped type
		if (type is NullableType) {
			type = ((NullableType)type).type;
			if (!node.isSafeDereference) {
				node.obj = InsertCast(node.obj, type);
			}
		} else if (node.isSafeDereference) {
			log.ErrorBadSaveDereference(node.location, type);
			return null;
		}
		
		// Perform the symbol lookup
		if (type is ClassType) {
			node.symbol = ((ClassType)type).def.block.scope.Lookup(node.name, kind);
			if (node.symbol == null) {
				log.ErrorBadMemberAccess(node);
			} else {
				node.computedType = node.symbol.type;
			}
		} else {
			log.ErrorBadMemberAccess(node);
		}
		
		// Perform overload resolution using information we were provided
		ResolveOverloads(node, argTypes);
		
		// For nullable safe dereference, the result could be null
		if (node.isSafeDereference && !(node.computedType is ErrorType)) {
			node.computedType = node.computedType.AsNullableType();
		}
		
		return null;
	}
	
	public override Null Visit(IndexExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		if (!(node.obj.computedType is ListType)) {
			log.ErrorTypeMismatch(node.location, new ListType(), node.obj.computedType);
			return null;
		}
		if (!node.index.computedType.IsInt()) {
			log.ErrorTypeMismatch(node.location, new PrimType { kind = PrimKind.Int }, node.index.computedType);
			return null;
		}
		node.computedType = node.obj.computedType.ItemType();
		
		return null;
	}
	
	public override Null Visit(NullableExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		if (node.value.computedType is MetaType) {
			Type type = node.value.computedType.InstanceType();
			if (type is NullableType) {
				log.ErrorBadNullableType(node.value.location, node.value.computedType);
			} else {
				node.computedType = new MetaType { instanceType = type.AsNullableType() };
			}
		} else {
			log.ErrorNotUseableType(node.value.location, node.value.computedType);
		}
		
		return null;
	}
	
	public override Null Visit(ReturnStmt node)
	{
		Type returnType = node.info.funcDef.symbol.type.ReturnType();
		context = new Context { targetType = returnType };
		base.Visit(node);
		if ((node.value == null) != (returnType is VoidType)) {
			log.ErrorVoidReturn(node.location, returnType is VoidType);
		} else if (node.value != null && !node.value.computedType.EqualsType(returnType)) {
			if (node.value.computedType.CanImplicitlyConvertTo(returnType)) {
				node.value = InsertCast(node.value, returnType);
			} else {
				log.ErrorTypeMismatch(node.location, returnType, node.value.computedType);
			}
		}
		return null;
	}
	
	public override Null Visit(ExprStmt node)
	{
		base.Visit(node);
		if (node.value.computedType is MetaType) {
			log.ErrorMetaTypeExpr(node.location);
		}
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		// Make sure the variable is defined (it will already be defined if it's
		// a global variable but not if it's a local variable)
		if (node.symbol == null) {
			node.symbol = new Symbol {
				kind = SymbolKind.Variable,
				isStatic = false,
				def = node,
				type = new ErrorType()
			};
			scope.Define(node.symbol);
		}
		
		if (node.type is VarExpr && node.value != null) {
			// Handle type inference
			node.value.Accept(this);
			if (node.value.computedType is NullType || node.value.computedType is VoidType) {
				log.ErrorNotUseableType(node.location, new MetaType { instanceType = node.value.computedType });
				node.symbol.type = new ErrorType();
			} else {
				node.symbol.type = node.value.computedType;
			}
		} else {
			// Handle normal variable declaration
			node.type.Accept(this);
			if (!node.type.computedType.IsInstantiatableType()) {
				log.ErrorNotUseableType(node.type.location, node.type.computedType);
			} else {
				node.symbol.type = node.type.computedType.InstanceType();
				if (node.value != null) {
					// Provide the variable type as the context to resolve the value type
					context = new Context { targetType = node.symbol.type };
					node.value.Accept(this);
					if (!node.value.computedType.EqualsType(node.symbol.type)) {
						if (node.value.computedType.CanImplicitlyConvertTo(node.symbol.type)) {
							node.value = InsertCast(node.value, node.symbol.type);
						} else {
							log.ErrorTypeMismatch(node.location, node.symbol.type, node.value.computedType);
						}
					}
				}
			}
		}

		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Special-case void types for return types
		if (node.returnType is TypeExpr && ((TypeExpr)node.returnType).type is VoidType) {
			node.returnType.computedType = new MetaType { instanceType = ((TypeExpr)node.returnType).type };
		} else {
			node.returnType.Accept(this);
		}
		VisitAll(node.argDefs);
		if (node.block != null) {
			node.block.Accept(this);
		}
		return null;
	}
	
	public override Null Visit(IfStmt node)
	{
		base.Visit(node);
		if (!node.test.computedType.IsBool()) {
			log.ErrorTypeMismatch(node.test.location, new PrimType { kind = PrimKind.Bool }, node.test.computedType);
		}
		return null;
	}
	
	public override Null Visit(WhileStmt node)
	{
		base.Visit(node);
		if (!node.test.computedType.IsBool()) {
			log.ErrorTypeMismatch(node.test.location, new PrimType { kind = PrimKind.Bool }, node.test.computedType);
		}
		return null;
	}
	
	private void ResolveOverloads(Expr node, List<Type> argTypes)
	{
		if (!(node.computedType is OverloadedFuncType)) {
			return;
		}
		
		if (argTypes == null) {
			log.ErrorNoOverloadContext(node.location);
			return;
		}
		
		// Try to resolve overloaded functions
		OverloadedFuncType overloadedType = (OverloadedFuncType)node.computedType;
		List<Symbol> exactMatches = new List<Symbol>();
		List<Symbol> implicitMatches = new List<Symbol>();
		
		// Try to match each overload
		foreach (Symbol symbol in overloadedType.overloads) {
			FuncType funcType = (FuncType)symbol.type;
			if (argTypes.MatchesExactly(funcType.argTypes)) {
				exactMatches.Add(symbol);
			} else if (argTypes.MatchesWithImplicitConversions(funcType.argTypes)) {
				implicitMatches.Add(symbol);
			}
		}
		
		// Pick the best-matching overload
		List<Symbol> matches = (exactMatches.Count > 0) ? exactMatches : implicitMatches;
		if (matches.Count > 1) {
			log.ErrorMultipleOverloadsFound(node.location, argTypes);
		} else if (matches.Count == 1) {
			node.computedType = matches[0].type;
			
			// Store the resolved symbol
			if (node is IdentExpr) {
				((IdentExpr)node).symbol = matches[0];
			} else if (node is MemberExpr) {
				((MemberExpr)node).symbol = matches[0];
			}
		}
	}
	
	private static Expr InsertCast(Expr value, Type target)
	{
		Type type = new MetaType { instanceType = target };
		return new CastExpr {
			location = value.location,
			value = value,
			target = new TypeExpr { type = type, computedType = type },
			computedType = target
		};
	}
	
	private bool IsValidCast(Type from, Type to)
	{
		return from.EqualsType(to) || from.CanImplicitlyConvertTo(to) || (from.IsNumeric() && to.IsNumeric());
	}
	
	private bool SetUpBinaryOpHelper(BinaryExpr node, bool resultIsBool)
	{
		Type left = node.left.computedType;
		Type right = node.right.computedType;
		
		if (left.EqualsType(right)) {
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : left;
			return true;
		}
		
		if (left.CanImplicitlyConvertTo(right)) {
			node.left = InsertCast(node.left, right);
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : right;
			return true;
		}
		
		if (right.CanImplicitlyConvertTo(left)) {
			node.right = InsertCast(node.right, left);
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : left;
			return true;
		}
		
		return false;
	}
	
	private bool SetUpBinaryOp(BinaryExpr node)
	{
		Type left = node.left.computedType;
		Type right = node.right.computedType;
		
		// Binary operators aren't supported on type literals
		if (left is MetaType || right is MetaType) {
			return false;
		}
		
		switch (node.op) {
			case BinaryOp.Assign:
				if (left.EqualsType(right)) {
					node.computedType = left;
					return true;
				} else if (right.CanImplicitlyConvertTo(left)) {
					node.right = InsertCast(node.right, left);
					node.computedType = left;
					return true;
				}
				break;
				
			case BinaryOp.NullableDefault:
				if (left is NullableType) {
					Type type = ((NullableType)left).type;
					if (right.EqualsType(type)) {
						node.computedType = type;
						return true;
					} else if (right.CanImplicitlyConvertTo(type)) {
						node.right = InsertCast(node.right, type);
						node.computedType = type;
						return true;
					}
				}
				break;
		
			case BinaryOp.And:
			case BinaryOp.Or:
				if (left.IsBool() && right.IsBool()) {
					node.computedType = new PrimType { kind = PrimKind.Bool };
					return true;
				}
				break;
		
			case BinaryOp.Add:
				if (((left.IsNumeric() && right.IsNumeric()) || (left.IsString() && right.IsString())) && SetUpBinaryOpHelper(node, false)) {
					return true;
				}
				break;
				
			case BinaryOp.Subtract:
			case BinaryOp.Multiply:
			case BinaryOp.Divide:
				if (left.IsNumeric() && right.IsNumeric() && SetUpBinaryOpHelper(node, false)) {
					return true;
				}
				break;
		
			case BinaryOp.LShift:
			case BinaryOp.RShift:
			case BinaryOp.BitAnd:
			case BinaryOp.BitOr:
			case BinaryOp.BitXor:
				if (left.IsInt() && right.IsInt() && SetUpBinaryOpHelper(node, false)) {
					return true;
				}
				break;

			case BinaryOp.Equal:
			case BinaryOp.NotEqual:
				if (SetUpBinaryOpHelper(node, true)) {
					return true;
				}
				break;
				
			case BinaryOp.LessThan:
			case BinaryOp.GreaterThan:
			case BinaryOp.LessThanEqual:
			case BinaryOp.GreaterThanEqual:
				if (((left.IsNumeric() && right.IsNumeric()) || (left.IsString() && right.IsString())) && SetUpBinaryOpHelper(node, true)) {
					return true;
				}
				break;
		}
		
		return false;
	}
}
