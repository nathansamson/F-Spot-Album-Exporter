//
// ThumbnailCache.cs
//
// Author:
//   Ettore Perazzoli <ettore@src.gnome.org>
//   Larry Ewing <lewing@novell.com>
//   Stephane Delcroix <sdelcroix@novell.com>
//
// Copyright (C) 2003-2008 Novell, Inc.
// Copyright (C) 2003 Ettore Perazzoli
// Copyright (C) 2004-2005 Larry Ewing
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
using System.Collections;
using Gdk;

using Hyena;
using FSpot.Utils;

namespace FSpot
{
public class ThumbnailCache : IDisposable {

	// Types.

	private class Thumbnail {
		// Uri of the image source
		public SafeUri uri;

		// The uncompressed thumbnail.
		public Pixbuf pixbuf;
	}


	// Private members and constants

	private const int DEFAULT_CACHE_SIZE = 2;

	private int max_count;
	private ArrayList pixbuf_mru;
	private Hashtable pixbuf_hash = new Hashtable ();

	static private ThumbnailCache defaultcache = new ThumbnailCache (DEFAULT_CACHE_SIZE);


	// Public API

	public ThumbnailCache (int max_count)
	{
		this.max_count = max_count;
		pixbuf_mru = new ArrayList (max_count);
	}

	static public ThumbnailCache Default {
		get {
			return defaultcache;
		}
	}

	public void AddThumbnail (SafeUri uri, Pixbuf pixbuf)
	{
		Thumbnail thumbnail = new Thumbnail ();

		thumbnail.uri = uri;
		thumbnail.pixbuf = pixbuf;

		RemoveThumbnailForUri (uri);

		pixbuf_mru.Insert (0, thumbnail);
		pixbuf_hash.Add (uri, thumbnail);

		MaybeExpunge ();
	}

	public Pixbuf GetThumbnailForUri (SafeUri uri)
	{
		if (! pixbuf_hash.ContainsKey (uri))
			return null;

		Thumbnail item = pixbuf_hash [uri] as Thumbnail;

		pixbuf_mru.Remove (item);
		pixbuf_mru.Insert (0, item);

        if (item.pixbuf == null)
            return null;
        return item.pixbuf.ShallowCopy ();
	}

	public void RemoveThumbnailForUri (SafeUri uri)
	{
		if (! pixbuf_hash.ContainsKey (uri))
			return;

		Thumbnail item = pixbuf_hash [uri] as Thumbnail;

		pixbuf_hash.Remove (uri);
		pixbuf_mru.Remove (item);

		item.pixbuf.Dispose ();
	}

	public void Dispose ()
	{
		foreach (object item in pixbuf_mru) {
			Thumbnail thumb = item as Thumbnail;
			pixbuf_hash.Remove (thumb.uri);
			thumb.pixbuf.Dispose ();
		}
		pixbuf_mru.Clear ();
		System.GC.SuppressFinalize (this);
	}

	~ThumbnailCache ()
	{
		Log.DebugFormat ("Finalizer called on {0}. Should be Disposed", GetType ());
		foreach (object item in pixbuf_mru) {
			Thumbnail thumb = item as Thumbnail;
			pixbuf_hash.Remove (thumb.uri);
			thumb.pixbuf.Dispose ();
		}
		pixbuf_mru.Clear ();
	}

	// Private utility methods.

	private void MaybeExpunge ()
	{
		while (pixbuf_mru.Count > max_count) {
			Thumbnail thumbnail = pixbuf_mru [pixbuf_mru.Count - 1] as Thumbnail;

			pixbuf_hash.Remove (thumbnail.uri);
			pixbuf_mru.RemoveAt (pixbuf_mru.Count - 1);

			thumbnail.pixbuf.Dispose ();
		}
	}
}
}
