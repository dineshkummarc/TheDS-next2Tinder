using System;

public class StructuralCheckPass : DefaultVisitor
{
	private Log log;
	
	public StructuralCheckPass(Log log)
	{
		this.log = log;
	}
	
	private string NameForStmt(Stmt stmt)
	{
		if (stmt is IfStmt) {
			return "if statement";
		}
		if (stmt is ReturnStmt) {
			return "return";
		}
		if (stmt is ExprStmt) {
			return "free expression";
		}
		if (stmt is ExternalStmt) {
			return "external block";
		}
		if (stmt is WhileStmt) {
			return "while block";
		}
		if (stmt is VarDef) {
			return "variable";
		}
		if (stmt is FuncDef) {
			return "function";
		}
		if (stmt is ClassDef) {
			return "class";
		}
		return "statement";
	}
	
	public override Null Visit(Block node)
	{
		// Provide errors for forbidden statements
		if (node.info.funcDef != null) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is VarDef || stmt is ExprStmt || stmt is IfStmt || stmt is ReturnStmt || stmt is WhileStmt) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a function body");
			}
		} else if (node.info.classDef != null) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a class definition");
			}
		} else if (node.info.inExternal) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside an external block");
			}
		} else {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ExternalStmt || stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "at module scope");
			}
		}
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		if (node.value != null) {
			if (node.info.inExternal) {
				log.ErrorStmtNotAllowed(node.location, "initialized variable", "inside an external block");
			} else if (node.info.classDef == null && node.info.funcDef == null) {
				log.ErrorStmtNotAllowed(node.location, "initialized variable", "at module scope");
			}
		}
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Forbid default arguments for the moment
		foreach (VarDef arg in node.argDefs) {
			if (arg.value != null) {
				log.ErrorDefaultArgNotAllowed(arg.location);
			}
		}
		
		// Validate the presence of the function body
		if (node.info.inExternal != (node.block == null)) {
			log.ErrorFunctionBody(node.location, node.info.inExternal);
		}
		
		base.Visit(node);
		return null;
	}
}
