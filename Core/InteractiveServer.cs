using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;

public class InteractiveServer
{
	private const int port = 8080;
	private const string htmlExample = htmlNullable;
	private const string htmlNullable = @"
external {
  bool something()
  void use(int a)
}

int test0() {
  int? a
  // return a // warning
  return 0
}

void test1(int? a) {
  if a == null {
    // use(a) // warning
  } else {
    use(a)
  }
}

void test2(int? a, int? b) {
  if a == null or b == null {
    // use(a) // warning
    // use(b) // warning
  } else {
    use(a)
    use(b)
  }
}

void test3(int? a, int? b) {
  if a != null or b != null {
    // use(b) // warning
    if a == null {
      use(b)
    }
  }
}

int test4(int? a) {
  if a != null { return a }
  return 0 // error without this
}

int test5(int? a) {
  if a != null { return a }
  if a == null { return 0 }
  // return 0 // warning
}

void test6(int? a, int? b) {
  if a != null {
    b = a
    if a != null { use(a) }
    // if a == null { use(0) } // warning
  }
}

void test7(int? a) {
  int? b
  if a != null {
    b = null
  } else {
    b = a
  }
  // use(b) // warning
}

int test8(int? a) {
  int? b
  if a != null {
    b = a
  } else {
    b = null
  }
  if a != null {
    return b
  } else {
    return 0
  }
}

void test9(int? a) {
  int? b = 1
  if a != null {
    b = null
  } else {
  }
  // use(b) // warning
}

void test10(int? a, int? b) {
  b = null
  if something() {
    a = 1
    b = 1
  }
  if b != null {
    use(a)
  }
}

void test11() {
  int? a, b, c, d
  while a == null or b == null or c == null {
    if a == null { a = 1 }
    else if b == null { b = 2 }
    else if c == null { c = 3 }
    // else if d == null { d = 4 } // warning
  }
  use(c)
  // use(d) // warning
}

void test12(int? a, int? b) {
  a = b = null
  // use(a) // warning
  // use(b) // warning
}
";
	private const string htmlMap = @"
external {
  int length(list<int> items)
  void add(list<int> items, int item)
}

list<int> map(list<int> from, function<int, int> func) {
  list<int> to = []
  int i = 0
  while i < length(from) {
    add(to, func(from[i]))
    i = i + 1
  }
  return to
}

int square(int x) {
  return x * x
}

void main() {
  list<int> result = map([1, 2, 3], square)
}
";
	private const string htmlRename = @"
class Object {
  int _const
  void const() {}
  void const(int x) {}
  void const(float x, bool y) {}
  void const(Object x) {}
  void const(list<string> x) {}
  void const(list<list<string>> x) {}
}

void main() {
  Object obj = Object()
  obj.const()
  obj.const(1)
  obj.const(1, true)
}

class Visitor {
  void visit(A node) {}
  void visit(B node) {}
}

class A { void accept(Visitor visitor) { visitor.visit(this) } }
class B { void accept(Visitor visitor) { visitor.visit(this) } }
";
	private const string htmlLinkList = @"
external {
  void print(string text)
  string str(int x)
}

class Link {
  int value
  Link? next
}

Link cons(int value, Link? next) {
  Link link = Link()
  link.value = value
  link.next = next
  return link
}

string printHelper(Link? link) {
  if link != null {
    string text = str(link.value)
    if link.next != null { text = text + "", "" }
    return text + printHelper(link.next)
  }
  return """"
}

void print(Link link) {
  print(""["" + printHelper(link) + ""]"")
}

void main() {
  Link nums = cons(1, cons(2, cons(3, null)))
  print(nums)
}
";
	private const string htmlVector = @"
external {
  void print(string text)
  string str(float x)
}

class Vector {
  float x, y

  static Vector new(float x, float y) {
    Vector v = Vector()
    v.x = x
    v.y = y
    return v
  }

  static Vector new(Vector o) {
    Vector v = Vector()
    v.x = o.x
    v.y = o.y
    return v
  }

  Vector plus(Vector vec) {
    return Vector.new(this.x + vec.x, this.y + vec.y)
  }
}

string str(Vector v) {
  return ""Vector("" + str(v.x) + "", "" + str(v.y) + "")""
}

Vector a

void main() {
  a = Vector.new(1, 2)
  Vector b = Vector.new(3, 4)
  Vector c = Vector.new(a.plus(b))
  print(str(a) + "" + "" + str(b) + "" = "" + str(c))
}
";
	private const string html = @"
		<!DOCTYPE html>
		<html><head>
			<title>Interactive Compiler</title>
		</head><body>
			<style>
				body { font: 13px Arial; margin: 30px; }
				textarea, pre { font: 12px Inconsolata, Consolas, monospace; }
			</style>
			<textarea id='input' rows='20' cols='100'>%s</textarea>
			<div id='output'></div>
			<script>

				var input = document.getElementById('input');
				var output = document.getElementById('output');
				var latest = 0;

				function text2html(text) {
					return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
				}

				function ajaxCompile(text, successFunc) {
					latest = text;
					var request = new XMLHttpRequest();
					request.onreadystatechange = function() {
						if (request.readyState == 4 && latest == text) {
							successFunc(request.responseXML.firstChild);
						}
					};
					request.open('POST', '/compile', true);
					request.send(text);
				}

				input.oninput = function() {
					ajaxCompile(input.value, function(xml) {
						var html = '';
						for (var node = xml.firstChild; node; node = node.nextSibling) {
							var contents = '';
							for (var child = node.firstChild; child; child = child.nextSibling) {
								contents += child.textContent + '\n';
							}
							if (contents.length) {
								html += '<br><br><b>' + text2html(node.tagName) + '</b><br><pre>' + text2html(contents) + '</pre>';
							}
						}
						output.innerHTML = html;
					});
				};

				input.focus();
				input.oninput();

			</script>
		</body></html>
	";
	private HttpListener server;
	
	public void Serve()
	{
		string host = "http://localhost:" + port + "/";
		server = new HttpListener();
		server.Prefixes.Add(host);
		server.Start();
		Console.WriteLine("serving on " + host);
		while (true) {
			HttpListenerContext context = server.GetContext();
			Process(context);
		}
	}
	
	private static void Process(HttpListenerContext context)
	{
		StreamReader input = new StreamReader(context.Request.InputStream);
		StreamWriter output = new StreamWriter(context.Response.OutputStream);
		switch (context.Request.Url.AbsolutePath) {
			case "/":
				context.Response.ContentType = "text/html";
				output.Write(html.Replace("%s", htmlExample));
				output.Close();
				break;
			case "/compile":
				context.Response.ContentType = "text/xml";
				output.Write(Compile(input.ReadToEnd()));
				output.Close();
				break;
		}
	}
	
	private static string Compile(string input)
	{
		Func<XmlDocument, XmlNode, string, XmlNode> createChild = (document, parent, name) => {
			XmlNode node = document.CreateElement(name);
			if (parent != null) {
				parent.AppendChild(node);
			} else {
				document.AppendChild(node);
			}
			return node;
		};
		Action<XmlDocument, XmlNode, string> appendText = (document, parent, text) => {
			parent.AppendChild(document.CreateTextNode(text));
		};
		
		XmlDocument doc = new XmlDocument();
		XmlNode xmlResults = createChild(doc, null, "Results");
		XmlNode xmlErrors = createChild(doc, xmlResults, "Errors");
		XmlNode xmlWarnings = createChild(doc, xmlResults, "Warnings");
		XmlNode xmlJs = createChild(doc, xmlResults, "Js");
		XmlNode xmlCpp = createChild(doc, xmlResults, "Cpp");
		XmlNode xmlTree = createChild(doc, xmlResults, "Tree");
		XmlNode xmlTokens = createChild(doc, xmlResults, "Tokens");
		List<Token> tokens = new List<Token>();
		Module module = null;
		Log log = new Log();
		
		try {
			tokens = Tokenizer.Tokenize(log, "<stdin>", input);
			if (log.errors.Count == 0) {
				module = Parser.Parse(log, tokens, "<stdin>");
				if (module != null) {
					bool compiled = Compiler.Compile(log, module);
					appendText(doc, xmlTree, module.Accept(new NodeToStringVisitor()));
					if (compiled) {
						appendText(doc, xmlJs, JsTarget.Generate(module));
						
						// Recompile because cloning isn't implemented yet
						Log temp = new Log();
						module = Parser.Parse(temp, tokens, "<stdin>");
						Compiler.Compile(temp, module);
						appendText(doc, xmlCpp, CppTarget.Generate(module));
					}
				}
			}
		} catch (Exception e) {
			log.errors.Add(e.ToString());
		}
		
		foreach (string error in log.errors) {
			appendText(doc, createChild(doc, xmlErrors, "Error"), error);
		}
		foreach (string error in log.warnings) {
			appendText(doc, createChild(doc, xmlWarnings, "Warning"), error);
		}
		foreach (Token token in tokens) {
			string text = token.text.Contains("\n") ? token.text.ToQuotedString() : token.text;
			appendText(doc, createChild(doc, xmlTokens, "Token"), token.kind + " " + text);
		}
		
		StringWriter output = new StringWriter();
		doc.WriteTo(new XmlTextWriter(output));
		return output.ToString();
	}
	
	public static void Main(string[] args)
	{
		new InteractiveServer().Serve();
	}
}

public class NodeToStringVisitor : Visitor<string>
{
	private string indent = "";
	
	private void Indent()
	{
		indent += "  ";
	}
	
	private void Dedent()
	{
		indent = indent.Substring(2);
	}
	
	private string Field(string name, string text)
	{
		return indent + name + " = " + (text != null ? text : "null") + "\n";
	}
	
	private string Field(string name, Node node)
	{
		return indent + name + " = " + (node != null ? node.Accept(this) : "null") + "\n";
	}
	
	private string Field(string name, List<string> list)
	{
		if (list.Count == 0) {
			return indent + name + " = {}\n";
		}
		return indent + name + " = { " + list.Join(", ") + " }\n";
	}
	
	private string Field<T>(string name, List<T> nodes) where T : Node
	{
		if (nodes.Count == 0) {
			return indent + name + " = {}\n";
		}
		Indent();
		string items = nodes.ConvertAll(x => indent + x.Accept(this) + "\n").Join();
		Dedent();
		return indent + name + " = {\n" + items + indent + "}\n";
	}
	
	private string Wrap(string name, string fields)
	{
		return name + " {\n" + fields + indent + "}";
	}
	
	public override string Visit(Block node)
	{
		if (node.stmts.Count == 0) {
			return "Block {}";
		}
		string text = "Block {\n";
		Indent();
		foreach (Stmt stmt in node.stmts) {
			text += indent + stmt.Accept(this) + "\n";
		}
		Dedent();
		return text + indent + "}";
	}
	
	public override string Visit(Module node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("block", node.block);
		Dedent();
		return Wrap("Module", fields);
	}
	
	public override string Visit(IfStmt node)
	{
		Indent();
		string fields = Field("test", node.test) + Field("thenBlock", node.thenBlock) + Field("elseBlock", node.elseBlock);
		Dedent();
		return Wrap("IfStmt", fields);
	}
	
	public override string Visit(ReturnStmt node)
	{
		Indent();
		string fields = Field("value", node.value);
		Dedent();
		return Wrap("ReturnStmt", fields);
	}
	
	public override string Visit(ExprStmt node)
	{
		Indent();
		string fields = Field("value", node.value);
		Dedent();
		return Wrap("ExprStmt", fields);
	}
	
	public override string Visit(ExternalStmt node)
	{
		Indent();
		string fields = Field("block", node.block);
		Dedent();
		return Wrap("ExternalStmt", fields);
	}
	
	public override string Visit(WhileStmt node)
	{
		Indent();
		string fields = Field("test", node.test) + Field("block", node.block);
		Dedent();
		return Wrap("WhileStmt", fields);
	}
	
	public override string Visit(VarDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("type", node.type) + Field("value", node.value);
		Dedent();
		return Wrap("VarDef", fields);
	}
	
	public override string Visit(FuncDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("isStatic", node.isStatic ? "true" : "false") +
			Field("returnType", node.returnType) + Field("argDefs", node.argDefs) + Field("block", node.block);
		Dedent();
		return Wrap("FuncDef", fields);
	}
	
	public override string Visit(ClassDef node)
	{
		Indent();
		string fields = Field("name", node.name) + Field("block", node.block);
		Dedent();
		return Wrap("ClassDef", fields);
	}
	
	public override string Visit(VarExpr node)
	{
		return "VarExpr {}";
	}
	
	public override string Visit(ThisExpr node)
	{
		return "ThisExpr {}";
	}
	
	public override string Visit(NullExpr node)
	{
		return "NullExpr {}";
	}
	
	public override string Visit(BoolExpr node)
	{
		return "BoolExpr { value = " + (node.value ? "true" : "false") + " }";
	}
	
	public override string Visit(IntExpr node)
	{
		return "IntExpr { value = " + node.value + " }";
	}
	
	public override string Visit(FloatExpr node)
	{
		return "FloatExpr { value = " + node.value + " }";
	}
	
	public override string Visit(StringExpr node)
	{
		return "StringExpr { value = " + node.value.ToQuotedString() + " }";
	}
	
	public override string Visit(IdentExpr node)
	{
		return "IdentExpr { name = " + node.name + " }";
	}
	
	public override string Visit(TypeExpr node)
	{
		return "TypeExpr { type = " + node.type + " }";
	}
	
	public override string Visit(ListExpr node)
	{
		string text = "ListExpr {\n";
		Indent();
		foreach (Expr expr in node.items) {
			text += indent + expr.Accept(this) + "\n";
		}
		Dedent();
		return text + indent + "}";
	}
	
	public override string Visit(UnaryExpr node)
	{
		Indent();
		string fields = Field("op", node.op.AsString()) + Field("value", node.value);
		Dedent();
		return Wrap("PrefixExpr", fields);
	}
	
	public override string Visit(BinaryExpr node)
	{
		Indent();
		string fields = Field("left", node.left) + Field("op", node.op.AsString()) + Field("right", node.right);
		Dedent();
		return Wrap("BinaryExpr", fields);
	}
	
	public override string Visit(CallExpr node)
	{
		Indent();
		string fields = Field("func", node.func) + Field("args", node.args);
		Dedent();
		return Wrap("CallExpr", fields);
	}
	
	public override string Visit(ParamExpr node)
	{
		Indent();
		string fields = Field("type", node.type) + Field("typeParams", node.typeParams);
		Dedent();
		return Wrap("ParamExpr", fields);
	}
	
	public override string Visit(CastExpr node)
	{
		Indent();
		string fields = Field("value", node.value) + Field("target", node.target);
		Dedent();
		return Wrap("CastExpr", fields);
	}
	
	public override string Visit(MemberExpr node)
	{
		Indent();
		string fields = Field("obj", node.obj) + Field("name", node.name) + Field("isNullable", node.isSafeDereference ? "true" : "false");
		Dedent();
		return Wrap("MemberExpr", fields);
	}
	
	public override string Visit(IndexExpr node)
	{
		Indent();
		string fields = Field("obj", node.obj) + Field("index", node.index);
		Dedent();
		return Wrap("IndexExpr", fields);
	}
	
	public override string Visit(NullableExpr node)
	{
		Indent();
		string fields = Field("value", node.value);
		Dedent();
		return Wrap("NullableExpr", fields);
	}
}
