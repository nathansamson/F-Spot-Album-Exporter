//
// ScrolledView.cs
//
// Author:
//   Larry Ewing <lewing@novell.com>
//
// Copyright (C) 2006 Novell, Inc.
// Copyright (C) 2006 Larry Ewing
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
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;
using FSpot.Utils;

namespace FSpot.Widgets {
	public class ScrolledView : Fixed {
		private EventBox ebox;
		private ScrolledWindow scroll;
		private DelayedOperation hide;

		public ScrolledView (IntPtr raw) : base (raw) {}

		public ScrolledView () : base () {
			scroll = new ScrolledWindow  (null, null);
			this.Put (scroll, 0, 0);
			scroll.Show ();
			
			//ebox = new BlendBox ();
			ebox = new EventBox ();
			this.Put (ebox, 0, 0);
			ebox.ShowAll ();
			
			hide = new DelayedOperation (2000, new GLib.IdleHandler (HideControls));
			this.Destroyed += HandleDestroyed;
		}

		public bool HideControls ()
		{
			return HideControls (false);
		}

		public bool HideControls (bool force)
		{
			int x, y;
			Gdk.ModifierType type;

			if (!force && IsRealized) {
				ebox.GdkWindow.GetPointer (out x, out y, out type);
				if (x < ebox.Allocation.Width && y < ebox.Allocation.Height) {
					hide.Start ();
					return true;
				}
			}

			hide.Stop ();
			ebox.Hide ();
			return false;
		}
		
		public void ShowControls ()
		{
			hide.Stop ();
			hide.Start ();
			ebox.Show ();
		}

		private void HandleDestroyed (object sender, System.EventArgs args)
		{
			hide.Stop ();
		}

		public EventBox ControlBox {
			get {
				return ebox;
			}
		}
		public ScrolledWindow ScrolledWindow {
			get {
				return scroll;
			}
		}

		protected override void OnSizeAllocated (Gdk.Rectangle allocation)
		{
			scroll.SetSizeRequest (allocation.Width, allocation.Height);
			base.OnSizeAllocated (allocation);
		}
	}
}
