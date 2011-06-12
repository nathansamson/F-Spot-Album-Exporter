//
// AlbumExport.cs
//
// Author:
//   Nathan Samson <nathansamson@gmail.com>
//
// Copyright (C) 2011 Nathan Samson
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
using System.IO;
using System.Collections.Generic;

using Gtk;

using FSpot;
using FSpot.Core;
using FSpot.Filters;
using FSpot.UI.Dialog;

namespace FSpot.Exporters.Album {
	public class AlbumExport : FSpot.Extensions.IExporter {
		IBrowsableCollection selection;
	
		[GtkBeans.Builder.Object] Gtk.Dialog dialog;
		ThreadProgressDialog progress_dialog;
		
		internal struct ExportProperties {
			private string path;
			public string Path {
				get { return path; }
			}

			private uint hq_size;
			public uint HQSize {
				get { return hq_size; }
			}

			private bool restrict_hq_size;
			public bool RestrictHQSize {
				get { return restrict_hq_size; }
			}
			
			public ExportProperties(string path, bool restrict_hq_size, uint hq_size)
			{
				this.path = path;
				this.restrict_hq_size = restrict_hq_size;
				this.hq_size = hq_size;
			}
		}

		internal struct ImageSize {
			private string path;
			public string Path {
				get { return path; }
			}

			private uint max_size;
			public uint MaxSize {
				get { return max_size; }
			}

			public ImageSize(uint max_size, string path) {
				this.max_size = max_size;
				this.path = path;
			}
		}
		
		ExportProperties export_properties;
	
		public void Run(IBrowsableCollection selection)
		{
			this.selection = selection;
		
			GtkBeans.Builder builder = new GtkBeans.Builder (null, "album_exporter.ui", null);
			dialog = new Gtk.Dialog (builder.GetRawObject ("exportDialog"));
			dialog.ShowAll();
			int response = dialog.Run();
			if (response != (int)Gtk.ResponseType.Ok) {
				dialog.Hide();
				return;
			}
			
			dialog.Hide();

			Gtk.FileChooserButton exportFileChooserButton = new Gtk.FileChooserButton (builder.GetRawObject ("exportDirectoryChooserButton"));
			Gtk.CheckButton restrictMaxSizeCheckButton = new Gtk.CheckButton (builder.GetRawObject ("restrictMaxSizeCheckButton"));
			uint hqMaxSize = 0;
			if (restrictMaxSizeCheckButton.Active) {
				Gtk.SpinButton hqSizeSpinButton = new Gtk.SpinButton (builder.GetRawObject ("hqMaxSizeSpinButton"));
				hqMaxSize = (uint)hqSizeSpinButton.ValueAsInt;
			}

			export_properties = new ExportProperties(exportFileChooserButton.Uri,
			                                         restrictMaxSizeCheckButton.Active, hqMaxSize);

			System.Threading.Thread command_thread = new System.Threading.Thread (new System.Threading.ThreadStart (Export));
			command_thread.Name = "Exporting Photos";

			progress_dialog = new ThreadProgressDialog (command_thread, 1);
			progress_dialog.Start ();
		}
		
		private void Export()
		{
			GLib.File destBasePath = GLib.FileFactory.NewForUri (export_properties.Path);

			if (!destBasePath.IsNative) {
				throw new NotImplementedException ("This is not yet implemented.");
			} else if (! destBasePath.Exists) {
				System.Console.WriteLine ("Path does not exist: {0}", destBasePath.Path);
			}

			List<ImageSize> imageSizes = new List<ImageSize> ();
			if (export_properties.RestrictHQSize) {
				imageSizes.Add (new ImageSize (export_properties.HQSize, "hq"));
			} else {
				imageSizes.Add (new ImageSize (0, "hq"));
			}
			imageSizes.Add (new ImageSize (1024, "mq"));
			imageSizes.Add (new ImageSize (300,  "thumbs"));

			foreach (ImageSize size in imageSizes) {
				GLib.File sizePath = destBasePath.GetChild(size.Path);
				if (!sizePath.Exists) {
					sizePath.MakeDirectory (null);
				}
			}

			for (int photo_index = 0; photo_index < selection.Count; photo_index++)
			{
				IPhoto photo = selection[photo_index];
				

				foreach (ImageSize size in imageSizes) {
					FilterSet filter_set = new FilterSet ();
					if (size.MaxSize > 0) {
						filter_set.Add(new ResizeFilter (size.MaxSize));
					}
					
					FilterRequest req = new FilterRequest (photo.DefaultVersion.Uri);
					filter_set.Convert (req);

					string file_name = photo_index.ToString () + System.IO.Path.GetExtension (photo.DefaultVersion.Filename);

					// TODO: Copy with Glib
					System.IO.File.Copy (req.Current.LocalPath,
					                     destBasePath.GetChild (size.Path).GetChild (file_name).Path, true);
				}
				progress_dialog.Fraction = (photo_index + 1) / (double) selection.Count;
			}
			progress_dialog.Hide();
		}
	}
}
