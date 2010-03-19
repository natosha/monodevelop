// 
// DomIndentationVisitor.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using MonoDevelop.CSharp.Dom;
using System.Text;
using MonoDevelop.Projects.Dom;
using Mono.TextEditor;
using MonoDevelop.Refactoring;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.CSharp.Formatting
{
	public class DomIndentationVisitor : AbtractCSharpDomVisitor<object, object>
	{
		CSharpFormattingPolicy policy;
		TextEditorData data;
		List<Change> changes = new List<Change> ();
		Indent curIndent = new Indent ();
		
		public int IndentLevel {
			get {
				return curIndent.Level;
			}
			set {
				curIndent.Level = value;
			}
		}
		
		public int CurrentSpaceIndents {
			get;
			set;
		}
		
		public DomIndentationVisitor (CSharpFormattingPolicy policy, TextEditorData data)
		{
			this.policy = policy;
			this.data = data;
		}
		
		public override object VisitCompilationUnit (MonoDevelop.CSharp.Dom.CompilationUnit unit, object data)
		{
			base.VisitCompilationUnit (unit, data);
			RefactoringService.AcceptChanges (null, null, changes);
			return null;
		}
		
		public override object VisitNamespaceDeclaration (NamespaceDeclaration namespaceDeclaration, object data)
		{
			FixIndentation (namespaceDeclaration.StartLocation);
			IndentLevel++;
			object result = base.VisitNamespaceDeclaration (namespaceDeclaration, data);
			IndentLevel--;
			FixIndentation (namespaceDeclaration.EndLocation);
			return result;
		}
		
		public override object VisitTypeDeclaration (TypeDeclaration typeDeclaration, object data)
		{
			FixIndentation (typeDeclaration.StartLocation);
			BraceStyle braceStyle;
			switch (typeDeclaration.ClassType) {
			case ClassType.Class:
				braceStyle = policy.ClassBraceStyle;
				break;
			case ClassType.Struct:
				braceStyle = policy.StructBraceStyle;
				break;
			case ClassType.Interface:
				braceStyle = policy.InterfaceBraceStyle;
				break;
			case ClassType.Enum:
				braceStyle = policy.EnumBraceStyle;
				break;
			default:
				throw new InvalidOperationException ("unsupported class type : " + typeDeclaration.ClassType);
			}
			EnforceBraceStyle (braceStyle, typeDeclaration.LBrace, typeDeclaration.RBrace);
			
			IndentLevel++;
			object result = base.VisitTypeDeclaration (typeDeclaration, data);
			IndentLevel--;
			return result;
		}
		
		public override object VisitPropertyDeclaration (PropertyDeclaration propertyDeclaration, object data)
		{
			FixIndentation (propertyDeclaration.StartLocation);
			EnforceBraceStyle (policy.PropertyBraceStyle, propertyDeclaration.LBrace, propertyDeclaration.RBrace);
			IndentLevel++;
			
			if (propertyDeclaration.GetAccessor != null && propertyDeclaration.GetAccessor.Body != null)
				EnforceBraceStyle (policy.PropertyGetBraceStyle, propertyDeclaration.GetAccessor.Body.LBrace, propertyDeclaration.GetAccessor.Body.RBrace);
			
			if (propertyDeclaration.SetAccessor != null && propertyDeclaration.SetAccessor.Body != null)
				EnforceBraceStyle (policy.PropertySetBraceStyle, propertyDeclaration.SetAccessor.Body.LBrace, propertyDeclaration.SetAccessor.Body.RBrace);
			
			object result = base.VisitPropertyDeclaration (propertyDeclaration, data);
			IndentLevel--;
			return result;
		}
		
		public override object VisitEventDeclaration (EventDeclaration eventDeclaration, object data)
		{
			FixIndentation (eventDeclaration.StartLocation);
			EnforceBraceStyle (policy.EventBraceStyle, eventDeclaration.LBrace, eventDeclaration.RBrace);
			IndentLevel++;
			
			if (eventDeclaration.AddAccessor != null && eventDeclaration.AddAccessor.Body != null)
				EnforceBraceStyle (policy.EventAddBraceStyle, eventDeclaration.AddAccessor.Body.LBrace, eventDeclaration.AddAccessor.Body.RBrace);
			
			if (eventDeclaration.RemoveAccessor != null && eventDeclaration.RemoveAccessor.Body != null)
				EnforceBraceStyle (policy.EventRemoveBraceStyle, eventDeclaration.RemoveAccessor.Body.LBrace, eventDeclaration.RemoveAccessor.Body.RBrace);
			
			IndentLevel--;
			return null;
		}

		
		public override object VisitAccessorDeclaration (Accessor accessorDeclaration, object data)
		{
			FixIndentation (accessorDeclaration.StartLocation);
			object result = base.VisitAccessorDeclaration (accessorDeclaration, data);
			return result;
		}
		
		public override object VisitMethodDeclaration (MethodDeclaration methodDeclaration, object data)
		{
			FixIndentation (methodDeclaration.StartLocation);
			if (methodDeclaration.Body != null)
				EnforceBraceStyle (policy.MethodBraceStyle, methodDeclaration.Body.LBrace, methodDeclaration.Body.RBrace);
			object result = base.VisitMethodDeclaration (methodDeclaration, data);
			return result;
		}
		
		public override object VisitConstructorDeclaration (ConstructorDeclaration constructorDeclaration, object data)
		{
			FixIndentation (constructorDeclaration.StartLocation);
			if (constructorDeclaration.Body != null)
				EnforceBraceStyle (policy.ConstructorBraceStyle, constructorDeclaration.Body.LBrace, constructorDeclaration.Body.RBrace);
			object result = base.VisitConstructorDeclaration (constructorDeclaration, data);
			return result;
		}
		
		public override object VisitDestructorDeclaration (DestructorDeclaration destructorDeclaration, object data)
		{
			FixIndentation (destructorDeclaration.StartLocation);
			if (destructorDeclaration.Body != null)
				EnforceBraceStyle (policy.DestructorBraceStyle, destructorDeclaration.Body.LBrace, destructorDeclaration.Body.RBrace);
			object result = base.VisitDestructorDeclaration (destructorDeclaration, data);
			return result;
		}
		
		#region Statements
		public override object VisitExpressionStatement (ExpressionStatement expressionStatement, object data)
		{
			FixStatementIndentation (expressionStatement.StartLocation);
			return null;
		}
		
		public override object VisitBlockStatement (BlockStatement blockStatement, object data)
		{
			FixIndentation (blockStatement.StartLocation);
			IndentLevel++;
			object result = base.VisitBlockStatement (blockStatement, data);
			IndentLevel--;
			FixIndentation (blockStatement.EndLocation, -1);
			return result;
		}
		
		public override object VisitBreakStatement (BreakStatement breakStatement, object data)
		{
			FixStatementIndentation (breakStatement.StartLocation);
			return null;
		}
		
		public override object VisitCheckedStatement (CheckedStatement checkedStatement, object data)
		{
			FixStatementIndentation (checkedStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.FixedBraceForcement , checkedStatement.EmbeddedStatement);
		}
		
		public override object VisitContinueStatement (ContinueStatement continueStatement, object data)
		{
			FixStatementIndentation (continueStatement.StartLocation);
			return null;
		}
		
		public override object VisitEmptyStatement (EmptyStatement emptyStatement, object data)
		{
			FixStatementIndentation (emptyStatement.StartLocation);
			return null;
		}
		
		public override object VisitFixedStatement (FixedStatement fixedStatement, object data)
		{
			FixStatementIndentation (fixedStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.FixedBraceForcement, fixedStatement.EmbeddedStatement);
		}
		
		public override object VisitForeachStatement (ForeachStatement foreachStatement, object data)
		{
			FixStatementIndentation (foreachStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.ForEachBraceForcement , foreachStatement.EmbeddedStatement);
		}

		object FixEmbeddedStatment (MonoDevelop.CSharp.Formatting.BraceStyle braceStyle, MonoDevelop.CSharp.Formatting.BraceForcement braceForcement, ICSharpNode node)
		{
			if (node == null)
				return null;
			bool isBlock = node is BlockStatement;
			switch (braceForcement) {
			case BraceForcement.DoNotChange:
				//nothing
				break;
			case BraceForcement.AddBraces:
				if (!isBlock) {
					int offset = data.Document.LocationToOffset (node.StartLocation.Line, node.StartLocation.Column);
					int start = SearchWhitespaceStart (offset);
					string startBrace = "";
					switch (braceStyle) {
					case BraceStyle.EndOfLineWithoutSpace:
						startBrace = "{";
						break;
					case BraceStyle.EndOfLine:
						startBrace = " {";
						break;
					case BraceStyle.NextLine:
						startBrace = data.EolMarker + curIndent.IndentString + "{";
						break;
					case BraceStyle.NextLineShifted2:
					case BraceStyle.NextLineShifted:
						startBrace = data.EolMarker + curIndent.IndentString + curIndent.SingleIndent + "{";
						break;
					}
					AddChange (start, offset - start, startBrace);
				}
				break;
			case BraceForcement.RemoveBraces:
				if (isBlock) {
					BlockStatement block = node as BlockStatement;
					if (block.Statements.Count () == 1) {
						int offset1 = data.Document.LocationToOffset (node.StartLocation.Line, node.StartLocation.Column);
						int start = SearchWhitespaceStart (offset1);
						
						int offset2 = data.Document.LocationToOffset (node.EndLocation.Line, node.EndLocation.Column);
						int end = SearchWhitespaceStart (offset2 - 1);
						
						AddChange (start, offset1 - start + 1, null);
						AddChange (end + 1, offset2 - end, null);
						node = (ICSharpNode)block.FirstChild;
						isBlock = false;
					}
				}
				break;
			}
			int originalLevel = curIndent.Level;
			if (isBlock) {
				BlockStatement block = node as BlockStatement;
				EnforceBraceStyle (braceStyle, block.LBrace, block.RBrace);
				if (braceStyle == BraceStyle.NextLineShifted2)
					curIndent.Level++;
			}
			
			if (!(node is IfElseStatement))
				curIndent.Level++;
			object result = isBlock ? base.VisitBlockStatement ((BlockStatement)node, null) : node.AcceptVisitor (this, null);
			curIndent.Level = originalLevel;
			switch (braceForcement) {
			case BraceForcement.DoNotChange:
				break;
			case BraceForcement.AddBraces:
				if (!isBlock) {
					int offset = data.Document.LocationToOffset (node.EndLocation.Line, node.EndLocation.Column);
					string startBrace = "";
					switch (braceStyle) {
					case BraceStyle.DoNotChange:
						startBrace = null;
						break;
					case BraceStyle.EndOfLineWithoutSpace:
						startBrace = data.EolMarker + curIndent.IndentString + "}";
						break;
					case BraceStyle.EndOfLine:
						startBrace = data.EolMarker + curIndent.IndentString + "}";
						break;
					case BraceStyle.NextLine:
						startBrace = data.EolMarker + curIndent.IndentString + "}";
						break;
					case BraceStyle.NextLineShifted2:
					case BraceStyle.NextLineShifted:
						startBrace = data.EolMarker + curIndent.IndentString + curIndent.SingleIndent + "}";
						break;
					}
					if (startBrace != null)
						AddChange (offset, 0, startBrace);
				}
				break;
			}
			return result;
		}
		
		void EnforceBraceStyle (MonoDevelop.CSharp.Formatting.BraceStyle braceStyle, ICSharpNode lbrace, ICSharpNode rbrace)
		{
			if (lbrace == null || rbrace == null)
				return;
//			LineSegment lbraceLineSegment = data.Document.GetLine (lbrace.StartLocation.Line);
			int lbraceOffset = data.Document.LocationToOffset (lbrace.StartLocation.Line, lbrace.StartLocation.Column);
			
//			LineSegment rbraceLineSegment = data.Document.GetLine (rbrace.StartLocation.Line);
			int rbraceOffset = data.Document.LocationToOffset (rbrace.StartLocation.Line, rbrace.StartLocation.Column);
			
			int whitespaceStart = SearchWhitespaceStart (lbraceOffset);
			int whitespaceEnd = SearchWhitespaceStart (rbraceOffset);
			string startIndent = "";
			string endIndent = "";
			switch (braceStyle) {
			case BraceStyle.DoNotChange:
				startIndent = endIndent = null;
				break;
			case BraceStyle.EndOfLineWithoutSpace:
				startIndent = "";
				endIndent = data.EolMarker + curIndent.IndentString;
				break;
			case BraceStyle.EndOfLine:
				startIndent = " ";
				endIndent = data.EolMarker + curIndent.IndentString;
				break;
			case BraceStyle.NextLine:
				startIndent = data.EolMarker + curIndent.IndentString;
				endIndent = data.EolMarker + curIndent.IndentString;
				break;
			case BraceStyle.NextLineShifted2:
			case BraceStyle.NextLineShifted:
				endIndent = startIndent = data.EolMarker + curIndent.IndentString + curIndent.SingleIndent;
				break;
			}
			
			if (startIndent != null) {
				AddChange (whitespaceStart, lbraceOffset - whitespaceStart, startIndent);
				AddChange (whitespaceEnd, rbraceOffset - whitespaceEnd, endIndent);
			}
		}
		
		void AddChange (int offset, int removedChars, string insertedText)
		{
			if (changes.Cast<DomSpacingVisitor.MyTextReplaceChange> ().Any (c => c.Offset == offset && c.RemovedChars == removedChars && c.InsertedText == insertedText))
				return;
			string currentText = data.Document.GetTextAt (offset, removedChars);
			if (currentText == insertedText)
				return;
			changes.Add (new DomSpacingVisitor.MyTextReplaceChange (data, offset, removedChars, insertedText));
		}
		
		int SearchWhitespaceStart (int startOffset)
		{
			for (int offset = startOffset - 1; offset >= 0; offset--) {
				char ch = data.Document.GetCharAt (offset);
				if (!Char.IsWhiteSpace (ch)) {
					return offset + 1;
				}
			}
			return startOffset - 1;
		}

		
		public override object VisitForStatement (ForStatement forStatement, object data)
		{
			FixStatementIndentation (forStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.ForBraceForcement, forStatement.EmbeddedStatement);
		}
		
		public override object VisitGotoStatement (GotoStatement gotoStatement, object data)
		{
			FixStatementIndentation (gotoStatement.StartLocation);
			return VisitChildren (gotoStatement, data);
		}
		
		public override object VisitIfElseStatement (IfElseStatement ifElseStatement, object data)
		{
			if (!(ifElseStatement.Parent is IfElseStatement))
				FixStatementIndentation (ifElseStatement.StartLocation);
			
			if (ifElseStatement.Condition != null)
				ifElseStatement.Condition.AcceptVisitor (this, data);
			
			if (ifElseStatement.TrueEmbeddedStatement != null)
				FixEmbeddedStatment (policy.StatementBraceStyle, policy.IfElseBraceForcement , ifElseStatement.TrueEmbeddedStatement);
			
			if (ifElseStatement.FalseEmbeddedStatement != null) {
				PlaceOnNewLine (policy.PlaceElseOnNewLine, ifElseStatement.ElseKeyword);
				if (ifElseStatement.FalseEmbeddedStatement is IfElseStatement) {
					PlaceOnNewLine (policy.PlaceElseIfOnNewLine, ((IfElseStatement)ifElseStatement.FalseEmbeddedStatement).IfKeyword);
				}
				FixEmbeddedStatment (policy.StatementBraceStyle, policy.IfElseBraceForcement, ifElseStatement.FalseEmbeddedStatement);
			}
			
			return null;
		}

		
		public override object VisitLabelStatement (LabelStatement labelStatement, object data)
		{
			// TODO
			return VisitChildren (labelStatement, data);
		}
		
		public override object VisitLockStatement (LockStatement lockStatement, object data)
		{
			FixStatementIndentation (lockStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.FixedBraceForcement , lockStatement.EmbeddedStatement);
		}
		
		public override object VisitReturnStatement (ReturnStatement returnStatement, object data)
		{
			FixStatementIndentation (returnStatement.StartLocation);
			return VisitChildren (returnStatement, data);
		}
		
		public override object VisitSwitchStatement (SwitchStatement switchStatement, object data)
		{
			FixStatementIndentation (switchStatement.StartLocation);
			EnforceBraceStyle (policy.StatementBraceStyle, switchStatement.LBrace, switchStatement.RBrace);
			object result = VisitChildren (switchStatement, data);
			return result;
		}
		
		public override object VisitSwitchSection (SwitchSection switchSection, object data)
		{
			
			if (policy.IndentCaseBody)
				curIndent.Level++;
			
			foreach (CaseLabel label in switchSection.CaseLabels) {
				FixStatementIndentation (label.StartLocation);
			}
			if (policy.IndentSwitchBody)
				curIndent.Level++;
			
			foreach (ICSharpNode stmt in switchSection.Statements) {
				stmt.AcceptVisitor (this, null);
			}
			if (policy.IndentSwitchBody)
				curIndent.Level--;
				
			if (policy.IndentCaseBody)
				curIndent.Level--;
			return null;
		}
		
		public override object VisitCaseLabel (CaseLabel caseLabel, object data)
		{
			// handled in switchsection
			return null;
		}
		
		public override object VisitThrowStatement (ThrowStatement throwStatement, object data)
		{
			FixStatementIndentation (throwStatement.StartLocation);
			return VisitChildren (throwStatement, data);
		}
		
		public override object VisitTryCatchStatement (TryCatchStatement tryCatchStatement, object data)
		{
			FixStatementIndentation (tryCatchStatement.StartLocation);
			
			if (tryCatchStatement.TryBlock != null)
				FixEmbeddedStatment (policy.StatementBraceStyle, BraceForcement.DoNotChange, tryCatchStatement.TryBlock);
			
			foreach (CatchClause clause in tryCatchStatement.CatchClauses) {
				PlaceOnNewLine (policy.PlaceCatchOnNewLine, clause.CatchKeyword);
				
				FixEmbeddedStatment (policy.StatementBraceStyle, BraceForcement.DoNotChange, clause.Block);
			}
			
			if (tryCatchStatement.FinallyBlock != null) {
				PlaceOnNewLine (policy.PlaceFinallyOnNewLine, tryCatchStatement.FinallyKeyword);
				
				FixEmbeddedStatment (policy.StatementBraceStyle, BraceForcement.DoNotChange, tryCatchStatement.FinallyBlock);
			}
			
			return VisitChildren (tryCatchStatement, data);
		}
		
		public override object VisitCatchClause (CatchClause catchClause, object data)
		{
			// Handled in TryCatchStatement
			return null;
		}
		
		public override object VisitUncheckedStatement (UncheckedStatement uncheckedStatement, object data)
		{
			FixStatementIndentation (uncheckedStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.FixedBraceForcement , uncheckedStatement.EmbeddedStatement);
		}
		
		public override object VisitUnsafeStatement (UnsafeStatement unsafeStatement, object data)
		{
			FixStatementIndentation (unsafeStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.FixedBraceForcement , unsafeStatement.Block);
		}
		
		public override object VisitUsingStatement (UsingStatement usingStatement, object data)
		{
			FixStatementIndentation (usingStatement.StartLocation);
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.UsingBraceForcement , usingStatement.EmbeddedStatement);
		}
		
		public override object VisitVariableDeclarationStatement (VariableDeclarationStatement variableDeclarationStatement, object data)
		{
			FixStatementIndentation (variableDeclarationStatement.StartLocation);
			return null;
		}
		
		public override object VisitWhileStatement (WhileStatement whileStatement, object data)
		{
			FixStatementIndentation (whileStatement.StartLocation);
			if (whileStatement.WhilePosition == WhilePosition.End) {
				PlaceOnNewLine (policy.PlaceWhileOnNewLine, whileStatement.WhileKeyword);
			}
				
			return FixEmbeddedStatment (policy.StatementBraceStyle, policy.WhileBraceForcement , whileStatement.EmbeddedStatement);
		}
		
		public override object VisitYieldStatement (YieldStatement yieldStatement, object data)
		{
			FixStatementIndentation (yieldStatement.StartLocation);
			return null;
		}
		
		#endregion
		
		void PlaceOnNewLine (bool newLine, ICSharpNode keywordNode)
		{
			if (keywordNode == null)
				return;
			int offset = data.Document.LocationToOffset (keywordNode.StartLocation.Line, keywordNode.StartLocation.Column);
			
			int whitespaceStart = SearchWhitespaceStart (offset);
			string indentString = newLine ? data.EolMarker + this.curIndent.IndentString : " ";
			AddChange (whitespaceStart, offset - whitespaceStart, indentString);
		}

		void FixStatementIndentation (MonoDevelop.Projects.Dom.DomLocation location)
		{
			int offset = data.Document.LocationToOffset (location.Line, location.Column);
			
			int whitespaceStart = SearchWhitespaceStart (offset);
			string indentString = data.EolMarker + this.curIndent.IndentString;
			AddChange (whitespaceStart, offset - whitespaceStart, indentString);
		}
		
		void FixIndentation (MonoDevelop.Projects.Dom.DomLocation location)
		{
			FixIndentation (location, 0);
		}
		
		void FixIndentation (MonoDevelop.Projects.Dom.DomLocation location, int relOffset)
		{
			LineSegment lineSegment = data.Document.GetLine (location.Line);
			string lineIndent = lineSegment.GetIndentation (data.Document);
			string indentString = this.curIndent.IndentString;
			if (indentString != lineIndent && location.Column + relOffset == lineIndent.Length) {
				AddChange (lineSegment.Offset, lineIndent.Length, indentString);
			}
		}
	}
}

