//
// AddinUpdateHandler.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
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
using System.Collections;
using System.IO;
using System.Threading;

using Gtk;

using MonoDevelop.Core;
using MonoDevelop.Core.AddIns.Setup;
using MonoDevelop.Core.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core.ProgressMonitoring;

namespace MonoDevelop.Ide.Gui
{
	class AddinUpdateHandler: CommandHandler
	{
		public static AggregatedProgressMonitor UpdateMonitor;
		AddinRepositoryEntry[] updates;
		static IStatusIcon updateIcon;
		
		public static void HideAlert ()
		{
			if (updateIcon != null) {
				updateIcon.Dispose ();
				updateIcon = null;
			}
		}
		
		protected override void Run ()
		{
			DateTime lastUpdate = Runtime.Properties.GetProperty ("MonoDevelop.Ide.AddinUpdater.LastUpdateCheck", DateTime.MinValue);
			TimeSpan updateSpan = Runtime.Properties.GetProperty ("MonoDevelop.Ide.AddinUpdater.UpdateSpan", TimeSpan.FromDays (1));
			
			if (lastUpdate + updateSpan <= DateTime.Now) {
				IProgressMonitor mon = IdeApp.Workbench.ProgressMonitors.GetBackgroundProgressMonitor ("Looking for add-in updates", "md-software-update"); 
				UpdateMonitor = new AggregatedProgressMonitor (mon);

				Thread t = new Thread (new ThreadStart (UpdateAddins));
				t.Start ();
			}
		}
		
		void UpdateAddins ()
		{
			using (UpdateMonitor) {
				Runtime.SetupService.UpdateRepositories (UpdateMonitor);
				updates = Runtime.SetupService.GetAvailableUpdates ();
				if (updates.Length > 0)
					Services.DispatchService.GuiDispatch (new MessageHandler (WarnAvailableUpdates));
			}
		}
		
		void WarnAvailableUpdates ()
		{
			updateIcon = IdeApp.Workbench.StatusBar.ShowStatusIcon (Services.Resources.GetBitmap ("md-software-update", IconSize.Menu));
			string s = GettextCatalog.GetString ("New add-in updates are available:");
			for (int n=0; n<updates.Length && n < 10; n++)
				s += "\n" + updates [n].Addin.Name;
			
			if (updates.Length > 10)
				s += "\n...";

			updateIcon.ToolTip = s;
			updateIcon.SetAlertMode (20);
			updateIcon.EventBox.ButtonPressEvent += new ButtonPressEventHandler (OnUpdateClicked);
		}
		
		void OnUpdateClicked (object s, ButtonPressEventArgs args)
		{
			if (args.Event.Button == 1) {
				HideAlert ();
				MonoDevelop.Core.Gui.Services.RunAddinManager ();
			}
		}
	}
}

