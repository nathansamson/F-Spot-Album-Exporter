//
// RetroactiveRoll.cs
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

using FSpot;
using FSpot.Core;
using FSpot.Extensions;
using System;
using Hyena;

using Hyena.Data.Sqlite;

namespace FSpot.Tools.RetroactiveRoll
{
	public class RetroactiveRoll: ICommand
	{
		public void Run (object o, EventArgs e)
		{
			Photo[] photos = App.Instance.Organizer.SelectedPhotos ();

			if (photos.Length == 0) {
				Log.Debug ("no photos selected, returning");
				return;
			}

			DateTime import_time = photos[0].Time;
			foreach (Photo p in photos)
				if (p.Time > import_time)
					import_time = p.Time;

			RollStore rolls = App.Instance.Database.Rolls;
			Roll roll = rolls.Create(import_time);
			foreach (Photo p in photos) {
				HyenaSqliteCommand cmd = new HyenaSqliteCommand ("UPDATE photos SET roll_id = ? " +
							       "WHERE id = ? ", roll.Id, p.Id);
				App.Instance.Database.Database.Execute (cmd);
				p.RollId = roll.Id;
			}
			Log.Debug ("RetroactiveRoll done: " + photos.Length + " photos in roll " + roll.Id);
		}
	}
}
