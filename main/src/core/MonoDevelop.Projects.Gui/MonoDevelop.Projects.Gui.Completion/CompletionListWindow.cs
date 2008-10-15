// CompletionListWindow.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2005 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using System.Collections.Generic;

using Gtk;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Core.Gui;

namespace MonoDevelop.Projects.Gui.Completion
{
	public class CompletionWindowManager
	{
		static CompletionListWindow wnd;
		
		static CompletionWindowManager ()
		{
			wnd = new CompletionListWindow ();
		}
		
		public static bool ShowWindow (char firstChar, ICompletionDataList list, ICompletionWidget completionWidget,
		                               ICodeCompletionContext completionContext, CompletionDelegate closedDelegate)
		{
			try {
				if (!wnd.ShowListWindow (firstChar, list,  completionWidget, completionContext, closedDelegate)) {
					if (list is IDisposable)
						((IDisposable)list).Dispose ();
					return false;
				}
				return true;
			} catch (Exception ex) {
				LoggingService.LogError (ex.ToString ());
				return false;
			}
		}
		
		public static bool ProcessKeyEvent (Gdk.Key key, Gdk.ModifierType modifier)
		{
			if (!wnd.Visible) return false;
			return wnd.ProcessKeyEvent (key, modifier);
		}
		
		public static void HideWindow ()
		{
			wnd.Hide ();
		}
	}
	
	internal class CompletionListWindow : ListWindow, IListDataProvider
	{
		internal ICompletionWidget completionWidget;
		ICodeCompletionContext completionContext;
		DeclarationViewWindow declarationviewwindow = new DeclarationViewWindow ();
		ICompletionData currentData;
		ICompletionDataList completionDataList;
		IMutableCompletionDataList mutableList;
		Widget parsingMessage;
		char firstChar;
		CompletionDelegate closedDelegate;
		int initialWordLength;
		
		const int declarationWindowMargin = 3;
		
		internal CompletionListWindow ()
		{
			SizeAllocated += new SizeAllocatedHandler (ListSizeChanged);
			WindowTransparencyDecorator.Attach (this);
		}
		
		public bool ProcessKeyEvent (Gdk.Key key, Gdk.ModifierType modifier)
		{
			ListWindow.KeyAction ka = ProcessKey (key, modifier);
			
			if ((ka & ListWindow.KeyAction.CloseWindow) != 0)
				Hide ();
				
			if ((ka & ListWindow.KeyAction.Complete) != 0) {
				UpdateWord ();
			}
			
			if ((ka & ListWindow.KeyAction.Ignore) != 0)
				return true;

			if ((ka & ListWindow.KeyAction.Process) != 0) {
				if (key == Gdk.Key.Left || key == Gdk.Key.Right) {
					// Close if there's a modifier active EXCEPT lock keys and Modifiers
					// Makes an exception for Mod1Mask (usually alt), shift and control
					// This prevents the window from closing if the num/scroll/caps lock are active
					// FIXME: modifier mappings depend on X server settings
					if ((modifier & ~(Gdk.ModifierType.LockMask | (Gdk.ModifierType.ModifierMask 
					    & ~Gdk.ModifierType.Mod1Mask & ~Gdk.ModifierType.ControlMask & ~Gdk.ModifierType.ShiftMask))
					) != 0) {
						Hide ();
						return false;
					}
					
					if (declarationviewwindow.Multiple) {
						if (key == Gdk.Key.Left)
							declarationviewwindow.OverloadLeft ();
						else
							declarationviewwindow.OverloadRight ();
						UpdateDeclarationView ();
					}
					return true;
				}
			}

			return false;
		}
		
		internal bool ShowListWindow (char firstChar, ICompletionDataList list, ICompletionWidget completionWidget,
		                              ICodeCompletionContext completionContext, CompletionDelegate closedDelegate)
		{
			if (mutableList != null) {
				mutableList.Changing -= OnCompletionDataChanging;
				mutableList.Changed -= OnCompletionDataChanged;
			}
			
			//initialWordLength = 0;
			this.completionDataList = list;
			this.completionContext = completionContext;
			this.closedDelegate = closedDelegate;
			mutableList = completionDataList as IMutableCompletionDataList;
			
			if (mutableList != null) {
				mutableList.Changing += OnCompletionDataChanging;
				mutableList.Changed += OnCompletionDataChanged;
			
				if (mutableList.IsChanging)
					OnCompletionDataChanging (null, null);
			}
			
			this.completionWidget = completionWidget;
			this.firstChar = firstChar;

			if (FillList (true)) {
				// makes control-space in midle of words to work
				string text = completionWidget.GetCompletionText (completionContext);
				if (text.Length == 0) {
					text = completionDataList.DefaultCompletionString;
					SelectEntry (text);
					initialWordLength = completionWidget.SelectedLength;
					return true;
				}
				
				initialWordLength = text.Length + completionWidget.SelectedLength;
				PartialWord = text; 
				//if there is only one matching result we take it by default
				if (completionDataList.AutoCompleteUniqueMatch && IsUniqueMatch && !IsChanging)
				{	
					UpdateWord ();
					Hide ();
				}
				return true;
			}
			else {
				Hide ();
				return false;
			}
		}
		
		bool FillList (bool reshow)
		{
			if ((completionDataList.Count == 0) && !IsChanging)
				return false;
			
			this.Style = completionWidget.GtkStyle;
			
			completionDataList.Sort ((ICompletionData a, ICompletionData b) => 
				((a.DisplayFlags & DisplayFlags.Obsolete) == (b.DisplayFlags & DisplayFlags.Obsolete))
					? string.Compare (a.DisplayText, b.DisplayText, true)
					: (a.DisplayFlags & DisplayFlags.Obsolete) != 0? 1 : -1);
			
			DataProvider = this;

			int x = completionContext.TriggerXCoord;
			int y = completionContext.TriggerYCoord;
			
			int w, h;
			GetSize (out w, out h);
			
			if ((x + w) > Screen.Width)
				x = Screen.Width - w;
			
			if ((y + h) > Screen.Height)
			{
				y = y - completionContext.TriggerTextHeight - h;
			}

			Move (x, y);
			
			if (reshow)
				Show ();
			return true;
		}
		
		void UpdateWord ()
		{
			if (Selection == -1 || SelectionDisabled)
				return;
			
			string word = currentData.CompletionText;
			IActionCompletionData ac = currentData as IActionCompletionData;
			if (ac != null) {
				ac.InsertCompletionText (completionWidget, completionContext);
				return;
			}
			int replaceLen = completionContext.TriggerWordLength + PartialWord.Length - initialWordLength;
			string pword = completionWidget.GetText (completionContext.TriggerOffset, completionContext.TriggerOffset + replaceLen);
			
			completionWidget.SetCompletionText (completionContext, pword, word);
		}
		
		public new void Hide ()
		{
			base.Hide ();
			declarationviewwindow.HideAll ();
			
			if (mutableList != null) {
				mutableList.Changing -= OnCompletionDataChanging;
				mutableList.Changed -= OnCompletionDataChanged;
				mutableList = null;
			}
			
			if (completionDataList != null) {
				if (completionDataList is IDisposable) {
					((IDisposable)completionDataList).Dispose ();
				}
				completionDataList = null;
			}
			
			if (closedDelegate != null) {
				closedDelegate ();
				closedDelegate = null;
			}
		}
		
		void ListSizeChanged (object obj, SizeAllocatedArgs args)
		{
			UpdateDeclarationView ();
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
		{
			bool ret = base.OnButtonPressEvent (evnt);
			if (evnt.Button == 1 && evnt.Type == Gdk.EventType.TwoButtonPress) {
				UpdateWord ();
				Hide ();
			}
			return ret;
		}
		
		protected override void OnSelectionChanged ()
		{
			base.OnSelectionChanged ();
			UpdateDeclarationView ();
		}
		
		void UpdateDeclarationView ()
		{
			if (completionDataList == null || List.Selection >= completionDataList.Count || List.Selection == -1)
				return;

			if (List.GdkWindow == null) return;
			Gdk.Rectangle rect = List.GetRowArea (List.Selection);
			int listpos_x = 0, listpos_y = 0;
			while (listpos_x == 0 || listpos_y == 0)
				GetPosition (out listpos_x, out listpos_y);
			int vert = listpos_y + rect.Y;
			
			int lvWidth = 0, lvHeight = 0;
			while (lvWidth == 0)
				this.GdkWindow.GetSize (out lvWidth, out lvHeight);
			if (vert >= listpos_y + lvHeight - 2) {
				vert = listpos_y + lvHeight - rect.Height;
			} else if (vert < listpos_y) {
				vert = listpos_y;
			}

			ICompletionData data = completionDataList[List.Selection];
			IOverloadedCompletionData overloadedData = data as IOverloadedCompletionData;

			string descMarkup = (data.DisplayFlags & DisplayFlags.DescriptionHasMarkup) != 0
				? data.Description
				: GLib.Markup.EscapeText (data.Description);

			declarationviewwindow.Hide ();
			
			if (data != currentData) {
				declarationviewwindow.Clear ();
				declarationviewwindow.Realize ();
	
				declarationviewwindow.AddOverload (descMarkup);

				if (overloadedData != null) {
					foreach (ICompletionData oData in overloadedData.GetOverloads ()) {
						bool oDataHasMarkup = (oData.DisplayFlags & DisplayFlags.DescriptionHasMarkup) != 0;
						declarationviewwindow.AddOverload (oDataHasMarkup
							? GLib.Markup.EscapeText (oData.Description)
							: oData.Description);
					}
				}
			}
			
			currentData = data;
			if (declarationviewwindow.DescriptionMarkup.Length == 0)
				return;

			int dvwWidth, dvwHeight;

			declarationviewwindow.Move (this.Screen.Width+1, vert);
			
			declarationviewwindow.SetFixedWidth (-1);
			declarationviewwindow.ReshowWithInitialSize ();
			declarationviewwindow.ShowAll ();
			declarationviewwindow.Multiple = (overloadedData != null && overloadedData.HasOverloads);

			declarationviewwindow.GdkWindow.GetSize (out dvwWidth, out dvwHeight);

			int horiz = listpos_x + lvWidth + declarationWindowMargin;
			if (this.Screen.Width - horiz >= lvWidth) {
				if (this.Screen.Width - horiz < dvwWidth)
					declarationviewwindow.SetFixedWidth (this.Screen.Width - horiz);
			} else {
				if (listpos_x - dvwWidth - declarationWindowMargin < 0) {
					declarationviewwindow.SetFixedWidth (listpos_x - declarationWindowMargin);
					dvwWidth = declarationviewwindow.SizeRequest ().Width;
				}
				horiz = listpos_x - dvwWidth - declarationWindowMargin;
			}

			declarationviewwindow.Move (horiz, vert);
		}
		
		#region IListDataProvider
		
		int IListDataProvider.ItemCount 
		{ 
			get { return completionDataList.Count; } 
		}
		
		string IListDataProvider.GetText (int n)
		{
			return completionDataList[n].DisplayText;
		}
		
		bool IListDataProvider.HasMarkup (int n)
		{
			return (completionDataList[n].DisplayFlags & DisplayFlags.Obsolete) != 0;
		}
		
		//NOTE: we only ever return markup for items marked as obsolete
		string IListDataProvider.GetMarkup (int n)
		{
			return "<s>" + GLib.Markup.EscapeText (completionDataList[n].DisplayText) + "</s>";
		}
		
		string IListDataProvider.GetCompletionText (int n)
		{
			return completionDataList[n].CompletionText;
		}
		
		Gdk.Pixbuf IListDataProvider.GetIcon (int n)
		{
			string iconName = completionDataList[n].Icon;
			if (string.IsNullOrEmpty (iconName))
				return null;
			return MonoDevelop.Core.Gui.Services.Resources.GetIcon (iconName, Gtk.IconSize.Menu);
		}
		
		#endregion
		
		internal bool IsChanging {
			get { return mutableList != null && mutableList.IsChanging; }
		}
		
		void OnCompletionDataChanging (object s, EventArgs args)
		{
			if (parsingMessage == null) {
				VBox box = new VBox ();
				box.PackStart (new Gtk.HSeparator (), false, false, 0);
				HBox hbox = new HBox ();
				hbox.BorderWidth = 3;
				hbox.PackStart (new Gtk.Image ("md-parser", Gtk.IconSize.Menu), false, false, 0);
				Gtk.Label lab = new Gtk.Label (GettextCatalog.GetString ("Gathering class information..."));
				lab.Xalign = 0;
				hbox.PackStart (lab, true, true, 3);
				hbox.ShowAll ();
				parsingMessage = hbox;
			}
			ShowFooter (parsingMessage);
		}
		
		void OnCompletionDataChanged (object s, EventArgs args)
		{
			//try to capture full selection state so as not to interrupt user
			string last = null;
			if (Visible) {
				if (SelectionDisabled)
					last = PartialWord;
				else
					last = CompleteWord;
			}

			HideFooter ();
			if (Visible) {
				//don't reset the user-entered word when refilling the list
				Reset (false);
				FillList (false);
				if (last != null)
					SelectEntry (last);
			}
		}
	}
	
	public delegate void CompletionDelegate ();
}
