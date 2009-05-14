// 
// SyntaxMode.cs
//  
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2009 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Mono.TextEditor.Highlighting;
using Mono.TextEditor;
using System.Xml;
using MonoDevelop.Projects;
using CSharpBinding;



namespace MonoDevelop.CSharpBinding
{
	public class CSharpSyntaxMode : Mono.TextEditor.Highlighting.SyntaxMode
	{
		public CSharpSyntaxMode ()
		{
			ResourceXmlProvider provider = new ResourceXmlProvider (typeof (IXmlProvider).Assembly, "CSharpSyntaxMode.xml");
			using (XmlReader reader = provider.Open ()) {
				SyntaxMode baseMode = SyntaxMode.Read (reader);
				this.rules = new List<Rule> (baseMode.Rules);
				this.keywords = new List<Keywords> (baseMode.Keywords);
				this.spans = new List<Span> (baseMode.Spans);
				this.matches = baseMode.Matches;
				this.prevMarker = new List<Marker> (baseMode.PrevMarker);
				this.SemanticRules = new List<SemanticRule> (baseMode.SemanticRules);
				this.table = baseMode.Table;
			}
			AddSemanticRule ("Comment", new HighlightUrlSemanticRule ("comment"));
			AddSemanticRule ("XmlDocumentation", new HighlightUrlSemanticRule ("comment"));
			AddSemanticRule ("String", new HighlightUrlSemanticRule ("string"));
//			AddSemanticRule (new HighlightPropertiesRule ());
		}
		
		public override SpanParser CreateSpanParser (Document doc, SyntaxMode mode, LineSegment line, Stack<Span> spanStack)
		{
			return new CSharpSpanParser (doc, mode, line, spanStack);
		}
		
		class IfBlockSpan : Span
		{
			public bool IsValid {
				get;
				private set;
			}
			
			public IfBlockSpan (bool isValid)
			{
				this.IsValid = isValid;
				TagColor = "text.preprocessor";
				if (!isValid) {
					Color    = "comment.block";
					Rule     = "text.preprocessor";
				} else {
					Color = "text";
					Rule = "<root>";
				}
				StopAtEol = false;
			}
			public override string ToString ()
			{
				return string.Format("[IfBlockSpan: IsValid={0}, Color={1}, Rule={2}]", IsValid, Color, Rule);
			}
		}
		
		class ElseIfBlockSpan : Span
		{
			public bool IsValid {
				get;
				private set;
			}
			
			public ElseIfBlockSpan (bool isValid)
			{
				this.IsValid = isValid;
				TagColor = "text.preprocessor";
				if (!isValid) {
					Color    = "comment.block";
					Rule     = "text.preprocessor";
				} else {
					Color = "text";
					Rule = "<root>";
				}
				StopAtEol = false;
			}
			public override string ToString ()
			{
				return string.Format("[ElseIfBlockSpan: IsValid={0}, Color={1}, Rule={2}]", IsValid, Color, Rule);
			}
		}
		
		class ElseBlockSpan : Span
		{
			public bool IsValid {
				get;
				private set;
			}
			
			public ElseBlockSpan (bool isValid)
			{
				this.IsValid = isValid;
				TagColor = "text.preprocessor";
				if (!isValid) {
					Color    = "comment.block";
					Rule     = "text.preprocessor";
				} else {
					Color = "text";
					Rule  = "<root>";
				}
				StopAtEol = false;
			}
			public override string ToString ()
			{
				return string.Format("[ElseBlockSpan: IsValid={0}, Color={1}, Rule={2}]", IsValid, Color, Rule);
			}
		}
		
		protected class CSharpSpanParser : SpanParser
		{
			class ConditinalExpressionEvaluator : ICSharpCode.NRefactory.Visitors.AbstractAstVisitor
			{
				HashSet<string> symbols = new HashSet<string> ();
				public ConditinalExpressionEvaluator ()
				{
					Project project = MonoDevelop.Ide.Gui.IdeApp.ProjectOperations.CurrentSelectedProject;
					if (project != null) {
						CSharpCompilerParameters cparams = ((DotNetProjectConfiguration)project.GetActiveConfiguration (project.ParentSolution.DefaultConfigurationId)).CompilationParameters as CSharpCompilerParameters;
						if (cparams != null) {
							string[] syms = cparams.DefineSymbols.Split (';');
							foreach (string s in syms) {
								string ss = s.Trim ();
								if (ss.Length > 0 && !symbols.Contains (ss))
									symbols.Add (ss);
							}
						}
					}
				}
				
				public override object VisitIdentifierExpression (ICSharpCode.NRefactory.Ast.IdentifierExpression identifierExpression, object data)
				{
					return symbols.Contains (identifierExpression.Identifier);
				}
				
				public override object VisitUnaryOperatorExpression (ICSharpCode.NRefactory.Ast.UnaryOperatorExpression unaryOperatorExpression, object data)
				{
					bool result = (bool)(unaryOperatorExpression.Expression.AcceptVisitor (this, data) ?? (object)false);
					if (unaryOperatorExpression.Op == ICSharpCode.NRefactory.Ast.UnaryOperatorType.Not)
						return !result;
					return result;
				}
				
				public override object VisitPrimitiveExpression (ICSharpCode.NRefactory.Ast.PrimitiveExpression primitiveExpression, object data)
				{
					return (bool)primitiveExpression.Value;
				}

				public override object VisitBinaryOperatorExpression (ICSharpCode.NRefactory.Ast.BinaryOperatorExpression binaryOperatorExpression, object data)
				{
					bool left  = (bool)(binaryOperatorExpression.Left.AcceptVisitor (this, data) ?? (object)false);
					bool right = (bool)(binaryOperatorExpression.Right.AcceptVisitor (this, data) ?? (object)false);
					
					switch (binaryOperatorExpression.Op) {
					case ICSharpCode.NRefactory.Ast.BinaryOperatorType.InEquality:
						return left != right;
					case ICSharpCode.NRefactory.Ast.BinaryOperatorType.Equality:
						return left == right;
					case ICSharpCode.NRefactory.Ast.BinaryOperatorType.LogicalOr:
						return left || right;
					case ICSharpCode.NRefactory.Ast.BinaryOperatorType.LogicalAnd:
						return left && right;
					}
					
					Console.WriteLine ("Unknown operator:" + binaryOperatorExpression.Op);
					return left;
				}
			}
			
			protected override void ScanSpan (ref int i)
			{
				if (i + 5 < doc.Length && doc.GetTextAt (i, 5) == "#else" && spanStack.Any (span => span is IfBlockSpan)) {
					bool previousResult = false;
					foreach (Span span in spanStack.ToArray ().Reverse ()) {
						if (span is IfBlockSpan) {
							previousResult = ((IfBlockSpan)span).IsValid;
						}
						if (span is ElseIfBlockSpan) {
							previousResult |= ((ElseIfBlockSpan)span).IsValid;
						}
					}
					
					LineSegment line = doc.GetLineByOffset (i);
					int length = line.Offset + line.EditableLength - i;
					while (spanStack.Count > 0 && !(CurSpan is IfBlockSpan)) {
						spanStack.Pop ();
					}
					IfBlockSpan ifBlock = (IfBlockSpan)CurSpan;
					
					ElseBlockSpan elseBlockSpan = new ElseBlockSpan (!previousResult);
					OnFoundSpanBegin (elseBlockSpan, i, 0);
					
					spanStack.Push (elseBlockSpan);
					ruleStack.Push (GetRule (elseBlockSpan));
					
					// put pre processor eol span on stack, so that '#else' gets the correct highlight
					OnFoundSpanBegin (preprocessorSpan, i, 1);
					spanStack.Push (preprocessorSpan);
					ruleStack.Push (preprocessorRule);
					i += length - 1;
					return;
				}
				if (CurRule.Name == "text.preprocessor" && i >= 3 && doc.GetTextAt (i - 3, 3) == "#if") {
					LineSegment line = doc.GetLineByOffset (i);
					int length = line.Offset + line.EditableLength - i;
					string parameter = doc.GetTextAt (i, length);
					ICSharpCode.NRefactory.Parser.CSharp.Lexer lexer = new ICSharpCode.NRefactory.Parser.CSharp.Lexer (new System.IO.StringReader (parameter));
					ICSharpCode.NRefactory.Ast.Expression expr = lexer.PPExpression ();
					
					bool result = !expr.IsNull ? (bool)expr.AcceptVisitor (new ConditinalExpressionEvaluator (), null) : false;
					
					IfBlockSpan ifBlockSpan = new IfBlockSpan (result);
					OnFoundSpanBegin (ifBlockSpan, i, length);
					i += length - 1;
					spanStack.Push (ifBlockSpan);
					ruleStack.Push (GetRule (ifBlockSpan));
					return;
				}
				if (i + 5 < doc.Length && doc.GetTextAt (i, 5) == "#elif" && spanStack.Any (span => span is IfBlockSpan)) {
					LineSegment line = doc.GetLineByOffset (i);
					int length = line.Offset + line.EditableLength - i;
					string parameter = doc.GetTextAt (i + 5, length - 5);
					
					ICSharpCode.NRefactory.Parser.CSharp.Lexer lexer = new ICSharpCode.NRefactory.Parser.CSharp.Lexer (new System.IO.StringReader (parameter));
					ICSharpCode.NRefactory.Ast.Expression expr = lexer.PPExpression ();
				
					bool result = !expr.IsNull ? (bool)expr.AcceptVisitor (new ConditinalExpressionEvaluator (), null) : false;
					
					if (result) {
						bool previousResult = false;
						foreach (Span span in spanStack.ToArray ().Reverse ()) {
							Console.WriteLine (span);
							if (span is IfBlockSpan) {
								previousResult = ((IfBlockSpan)span).IsValid;
							}
							if (span is ElseIfBlockSpan) {
								previousResult |= ((ElseIfBlockSpan)span).IsValid;
							}
						}
						Console.WriteLine ("prev:" + previousResult);
						result = !previousResult;
					}
					
					ElseIfBlockSpan elseIfBlockSpan = new ElseIfBlockSpan (result);
					OnFoundSpanBegin (elseIfBlockSpan, i, 0);
					
					spanStack.Push (elseIfBlockSpan);
					ruleStack.Push (GetRule (elseIfBlockSpan));
					
					// put pre processor eol span on stack, so that '#elif' gets the correct highlight
					OnFoundSpanBegin (preprocessorSpan, i, 1);
					spanStack.Push (preprocessorSpan);
					ruleStack.Push (preprocessorRule);
					//i += length - 1;
					return;
				}
				base.ScanSpan (ref i);
			}
			
			protected override bool ScanSpanEnd (Mono.TextEditor.Highlighting.Span cur, int i)
			{
				if (cur is IfBlockSpan || cur is ElseIfBlockSpan || cur is ElseBlockSpan) {
					bool end = i + 6 < doc.Length && doc.GetTextAt (i, 6) == "#endif";
					if (end) {
						OnFoundSpanEnd (cur, i, 0); // put empty end tag in
						while (!(spanStack.Peek () is IfBlockSpan)) {
							spanStack.Pop ();
							if (ruleStack.Count > 1) // rulStack[1] is always syntax mode
								ruleStack.Pop ();
						}
						spanStack.Pop ();
						if (ruleStack.Count > 1) // rulStack[1] is always syntax mode
							ruleStack.Pop ();
						// put pre processor eol span on stack, so that '#endif' gets the correct highlight
						foreach (Span span in mode.Spans) {
							if (span.Rule == "text.preprocessor") {
								OnFoundSpanBegin (span, i, 1);
								spanStack.Push (span);
								ruleStack.Push (GetRule (span));
								break;
							}
						}
					}
					return end;
				}
				return base.ScanSpanEnd (cur, i);
			}
			
			Span preprocessorSpan;
			Rule preprocessorRule;
			
			public CSharpSpanParser (Document doc, SyntaxMode mode, LineSegment line, Stack<Span> spanStack) : base (doc, mode, line, spanStack)
			{
				foreach (Span span in mode.Spans) {
					if (span.Rule == "text.preprocessor") {
						preprocessorSpan = span;
						preprocessorRule = GetRule (span);
					}
				}
			}
		}
	}
}
 