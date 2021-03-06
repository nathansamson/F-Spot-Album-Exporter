//
// Tag.cs
//
// Author:
//   Ruben Vermeersch <ruben@savanne.be>
//   Stephane Delcroix <sdelcroix@novell.com>
//
// Copyright (C) 2008-2010 Novell, Inc.
// Copyright (C) 2010 Ruben Vermeersch
// Copyright (C) 2008 Stephane Delcroix
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
using Gdk;
using FSpot.Utils;
using Hyena;

namespace FSpot.Core
{
	public class Tag : DbItem, IComparable<Tag>, IDisposable {
		string name;
		public string Name {
			get { return name; }
			set {  name = value;}
		}

		Category category;
		public Category Category {
			get { return category; }
			set {
				if (Category != null)
					Category.RemoveChild (this);

				category = value;
				if (category != null)
					category.AddChild (this);
			}
		}

		int sort_priority;
		public int SortPriority {
			get { return sort_priority; }
			set { sort_priority = value; }
		}

		int popularity = 0;
		public int Popularity {
			get { return popularity; }
			set { popularity = value; }
		}

		// Icon.  If theme_icon_name is not null, then we save the name of the icon instead
		// of the actual icon data.
		string theme_icon_name;
		public string ThemeIconName {
			get { return theme_icon_name; }
			set { theme_icon_name = value; }
		}

		Pixbuf icon;
		public Pixbuf Icon {
			get {
				if (icon == null && theme_icon_name != null) {
					cached_icon_size = IconSize.Hidden;
					icon = GtkUtil.TryLoadIcon (Global.IconTheme, theme_icon_name, 48, (Gtk.IconLookupFlags)0);
				}
				return icon;
			}
			set {
				theme_icon_name = null;
				if (icon != null)
					icon.Dispose ();
				icon = value;
				cached_icon_size = IconSize.Hidden;
				IconWasCleared = value == null;
			}
		}

		bool icon_was_cleared = false;
		public bool IconWasCleared {
			get { return icon_was_cleared; }
			set { icon_was_cleared = value; }
		}

		public enum IconSize {
			Hidden = 0,
			Small = 16,
			Medium = 24,
			Large = 48
		};

		static IconSize tag_icon_size = IconSize.Large;
		public static IconSize TagIconSize {
			get { return tag_icon_size; }
			set { tag_icon_size = value; }
		}

		Pixbuf cached_icon;
		private IconSize cached_icon_size = IconSize.Hidden;

		// We can use a SizedIcon everywhere we were using an Icon
		public Pixbuf SizedIcon {
			get {
				if (tag_icon_size == IconSize.Hidden) //Hidden
					return null;
				if (tag_icon_size == cached_icon_size)
					return cached_icon;
				if (theme_icon_name != null) { //Theme icon
					if (cached_icon != null)
						cached_icon.Dispose ();
					cached_icon = GtkUtil.TryLoadIcon (Global.IconTheme, theme_icon_name, (int) tag_icon_size, (Gtk.IconLookupFlags)0);

					if (Math.Max (cached_icon.Width, cached_icon.Height) <= (int) tag_icon_size)
						return cached_icon;
				}
				if (Icon == null)
					return null;

				if (Math.Max (Icon.Width, Icon.Height) >= (int) tag_icon_size) { //Don't upscale
					if (cached_icon != null)
						cached_icon.Dispose ();
					cached_icon = Icon.ScaleSimple ((int) tag_icon_size, (int) tag_icon_size, InterpType.Bilinear);
					cached_icon_size = tag_icon_size;
					return cached_icon;
				} else
					return Icon;
			}
		}


		// You are not supposed to invoke these constructors outside of the TagStore class.
		public Tag (Category category, uint id, string name)
			: base (id)
		{
			Category = category;
			Name = name;
		}


		// IComparer.
		public int CompareTo (Tag tag)
		{
			if (tag == null)
				throw new ArgumentNullException ("tag");

			if (Category == tag.Category) {
				if (SortPriority == tag.SortPriority)
					return Name.CompareTo (tag.Name);
				else
					return SortPriority - tag.SortPriority;
			} else {
				return Category.CompareTo (tag.Category);
			}
		}

		public bool IsAncestorOf (Tag tag)
		{
			if (tag == null)
				throw new ArgumentNullException ("tag");

			for (Category parent = tag.Category; parent != null; parent = parent.Category) {
				if (parent == this)
					return true;
			}

			return false;
		}

		public void Dispose ()
		{
			if (icon != null)
				icon.Dispose ();
			if (cached_icon != null)
				cached_icon.Dispose ();
			if (category != null)
				category.Dispose ();
			System.GC.SuppressFinalize (this);
		}

		~Tag ()
		{
			Log.DebugFormat ("Finalizer called on {0}. Should be Disposed", GetType ());
			if (icon != null)
				icon.Dispose ();
			if (cached_icon != null)
				cached_icon.Dispose ();
			if (category != null)
				category.Dispose ();
		}
	}
}
