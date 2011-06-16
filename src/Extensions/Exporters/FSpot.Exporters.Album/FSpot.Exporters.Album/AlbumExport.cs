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

		GtkBeans.Builder builder;
		Gtk.Dialog dialog;
		Gtk.Entry albumTitleEntry;
		Gtk.TextView albumDescriptionTextView;
		Gtk.FileChooserButton exportFileChooserButton;
		Gtk.CheckButton restrictMaxSizeCheckButton;
		Gtk.SpinButton hqSizeSpinButton;

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

			private string title;
			public string Title {
				get { return title; }
			}

			private string description;
			public string Description {
				get { return description; }
			}
			
			public ExportProperties(string path, bool restrict_hq_size, uint hq_size, string title, string description)
			{
				this.path = path;
				this.restrict_hq_size = restrict_hq_size;
				this.hq_size = hq_size;
				this.title = title;
				this.description = description;
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
		
			builder = new GtkBeans.Builder (null, "album_exporter.ui", null);
			albumTitleEntry = new Gtk.Entry (builder.GetRawObject ("albumTitleEntry"));
			albumDescriptionTextView = new Gtk.TextView (builder.GetRawObject ("albumDescriptionTextView"));
			exportFileChooserButton = new Gtk.FileChooserButton (builder.GetRawObject ("exportDirectoryChooserButton"));
			restrictMaxSizeCheckButton = new Gtk.CheckButton (builder.GetRawObject ("restrictMaxSizeCheckButton"));
			hqSizeSpinButton = new Gtk.SpinButton (builder.GetRawObject ("hqMaxSizeSpinButton"));
			restrictMaxSizeCheckButton.Toggled += delegate(object sender, EventArgs e) {
				hqSizeSpinButton.Sensitive = restrictMaxSizeCheckButton.Active;
			};

			dialog = new Gtk.Dialog (builder.GetRawObject ("exportDialog"));
			dialog.ShowAll();
			dialog.TransientFor = FSpot.App.Instance.Organizer.Window;
			if (! RunExportSettingsDialog ()) {
				return;
			}

			uint hqMaxSize = 0;
			if (restrictMaxSizeCheckButton.Active) {
				hqMaxSize = (uint)hqSizeSpinButton.ValueAsInt;
			}

			export_properties = new ExportProperties(exportFileChooserButton.Uri,
			                                         restrictMaxSizeCheckButton.Active, hqMaxSize,
			                                         albumTitleEntry.Text,
			                                         albumDescriptionTextView.Buffer.Text);

			System.Threading.Thread command_thread = new System.Threading.Thread (new System.Threading.ThreadStart (Export));
			command_thread.Name = "Exporting Photos";

			progress_dialog = new ThreadProgressDialog (command_thread, 1);
			progress_dialog.Start ();
		}

		private bool RunExportSettingsDialog ()
		{
			bool validated = false;
			while (! validated) {
				int response = dialog.Run ();

				if (response == (int)Gtk.ResponseType.Ok) {
					validated = true;
					if (albumTitleEntry.Text.Trim ().Length == 0) {
						validated = false;
						// TODO: Use InfoBar (Gtk+ 3) to display the error.
						Gtk.MessageDialog errorMessage = new Gtk.MessageDialog (dialog, DialogFlags.Modal, MessageType.Error,
						                                                        ButtonsType.Ok, "You should anter a Title.");
						errorMessage.Run ();
						errorMessage.Destroy ();
					}
				} else {
					dialog.Hide ();
					return false;
				}
			}
			dialog.Hide ();
			return true;
		}
		
		private void Export()
		{
			GLib.File destBasePath = GLib.FileFactory.NewForUri (export_properties.Path);

			if (!destBasePath.IsNative) {
				throw new NotImplementedException ("This is not yet implemented.");
			} else if (! destBasePath.Exists) {
				System.Console.WriteLine ("Path does not exist: {0}", destBasePath.Path);
			}

			ExportHtml (destBasePath);
			ExportPhotos (destBasePath);
		}

		private void ExportHtml(GLib.File destBasePath)
		{
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetCallingAssembly ();

			System.IO.StreamWriter inputStream = new System.IO.StreamWriter (new System.IO.MemoryStream ());
			System.Xml.XmlTextWriter writer = new System.Xml.XmlTextWriter (inputStream);
			writer.WriteStartDocument ();
			writer.WriteStartElement ("fspot-album");

			writer.WriteStartElement ("title");
			writer.WriteString (export_properties.Title);
			writer.WriteEndElement ();

			writer.WriteStartElement ("description");
			writer.WriteString (export_properties.Description);
			writer.WriteEndElement ();

			ExportResourceFile (destBasePath, assembly, "album.js", "DefaultTheme.album.js");

			writer.WriteStartElement ("scripts");

			writer.WriteStartElement ("script");
			writer.WriteAttributeString ("src", "https://ajax.googleapis.com/ajax/libs/jquery/1.6/jquery.min.js");
			writer.WriteEndElement ();

			writer.WriteStartElement ("script");
			writer.WriteAttributeString ("src", "album.js");
			writer.WriteEndElement ();

			writer.WriteEndElement ();

			// Stylesheets should be generated from a .theme (resource) file
			ExportResourceFile (destBasePath, assembly, "album.css", "DefaultTheme.album.css");
			ExportResourceFile (destBasePath, assembly, "dark.css", "DefaultTheme.dark.css");
			ExportResourceFile (destBasePath, assembly, "light.css", "DefaultTheme.light.css");
			writer.WriteStartElement ("stylesheets");

			writer.WriteStartElement ("stylesheet");
			writer.WriteAttributeString ("href", "album.css");
			writer.WriteEndElement ();

			writer.WriteStartElement ("stylesheet");
			writer.WriteAttributeString ("href", "dark.css");
			writer.WriteAttributeString ("title", "Dark");
			writer.WriteEndElement ();

			writer.WriteStartElement ("stylesheet");
			writer.WriteAttributeString ("href", "light.css");
			writer.WriteAttributeString ("alternate", "alternate");
			writer.WriteAttributeString ("title", "Light");
			writer.WriteEndElement ();

			writer.WriteEndElement ();

			writer.WriteStartElement ("photos");

			for (int photo_index = 0; photo_index < selection.Count; photo_index++)
			{
				IPhoto photo = selection[photo_index];

				writer.WriteStartElement ("photo");
				writer.WriteStartAttribute ("filename");
				string file_name = photo_index + System.IO.Path.GetExtension (photo.DefaultVersion.Filename);
				writer.WriteString (file_name);
				writer.WriteEndAttribute ();
				writer.WriteEndElement ();
			}

			writer.WriteEndElement ();

			writer.WriteEndElement ();
			writer.WriteEndDocument ();
			writer.Flush();
			writer.BaseStream.Seek (0, SeekOrigin.Begin);

			System.Xml.Xsl.XslTransform transform = new System.Xml.Xsl.XslTransform ();

			System.Xml.XmlReader xslt = System.Xml.XmlReader.Create (assembly.GetManifestResourceStream ("DefaultTheme.index.xsl"));
			transform.Load (xslt);

			System.Xml.XPath.XPathDocument input = new System.Xml.XPath.XPathDocument (writer.BaseStream);
			System.IO.Stream outputStream = new System.IO.FileStream (destBasePath.GetChild ("index.html").Path, System.IO.FileMode.Create);

			System.Xml.Xsl.XsltArgumentList argumentList = new System.Xml.Xsl.XsltArgumentList ();
			argumentList.AddParam ("createdByText", "", "Gallery Created By");
			argumentList.AddParam ("createdByPackage", "", FSpot.Core.Defines.PACKAGE);
			argumentList.AddParam ("createdByVersion", "", FSpot.Core.Defines.VERSION);

			transform.Transform (input, argumentList, outputStream);
			outputStream.Close ();



			transform = new System.Xml.Xsl.XslTransform ();
			
			xslt = System.Xml.XmlReader.Create (assembly.GetManifestResourceStream ("DefaultTheme.page.xsl"));
			transform.Load (xslt);
			for (int photo_index = 0; photo_index < selection.Count; photo_index++)
			{
				IPhoto photo = selection[photo_index];

				inputStream = new System.IO.StreamWriter (new System.IO.MemoryStream ());
				writer = new System.Xml.XmlTextWriter (inputStream);
				writer.WriteStartDocument ();
				writer.WriteStartElement ("fspot-image");

				writer.WriteStartElement ("fspot-album");

				writer.WriteStartElement ("title");
				writer.WriteString (export_properties.Title);
				writer.WriteEndElement ();

				writer.WriteStartElement ("description");
				writer.WriteString (export_properties.Description);
				writer.WriteEndElement ();

				writer.WriteEndElement ();
				
				writer.WriteStartElement ("title");
				writer.WriteString (photo.Name);
				writer.WriteEndElement ();
				
				writer.WriteStartElement ("description");
				writer.WriteString (photo.Description);
				writer.WriteEndElement ();

				writer.WriteStartElement ("filename");
				writer.WriteString (photo_index + System.IO.Path.GetExtension (photo.DefaultVersion.Filename));
				writer.WriteEndElement ();

				if (photo_index > 0) {
					writer.WriteStartElement ("prev");
					writer.WriteString ((photo_index).ToString());
					writer.WriteEndElement ();
				}

				if (photo_index < selection.Count - 1) {
					writer.WriteStartElement ("next");
					writer.WriteString ((photo_index + 2).ToString ());
					writer.WriteEndElement ();
				}

				writer.WriteStartElement ("scripts");

				writer.WriteStartElement ("script");
				writer.WriteAttributeString ("src", "https://ajax.googleapis.com/ajax/libs/jquery/1.6/jquery.min.js");
				writer.WriteEndElement ();
				
				writer.WriteStartElement ("script");
				writer.WriteAttributeString ("src", "album.js");
				writer.WriteEndElement ();
				
				writer.WriteEndElement ();

				
				// Stylesheets should be generated from a .theme (resource) file
				writer.WriteStartElement ("stylesheets");
				
				writer.WriteStartElement ("stylesheet");
				writer.WriteAttributeString ("href", "album.css");
				writer.WriteEndElement ();
				
				writer.WriteStartElement ("stylesheet");
				writer.WriteAttributeString ("href", "dark.css");
				writer.WriteAttributeString ("title", "Dark");
				writer.WriteEndElement ();

				writer.WriteStartElement ("stylesheet");
				writer.WriteAttributeString ("href", "light.css");
				writer.WriteAttributeString ("alternate", "alternate");
				writer.WriteAttributeString ("title", "Light");
				writer.WriteEndElement ();
				
				writer.WriteEndElement ();
				
				writer.WriteEndElement ();
				writer.WriteEndDocument ();
				writer.Flush ();
				writer.BaseStream.Seek (0, SeekOrigin.Begin);

				input = new System.Xml.XPath.XPathDocument (writer.BaseStream);
				outputStream = new System.IO.FileStream (destBasePath.GetChild ("img"+(photo_index + 1)+".html").Path, System.IO.FileMode.Create);

				transform.Transform (input, argumentList, outputStream);
				outputStream.Close ();
			}
		}

		private void ExportResourceFile (GLib.File destBasePath, System.Reflection.Assembly assembly, string local, string resource)
		{
			System.IO.Stream albumStream = new System.IO.FileStream (destBasePath.GetChild (local).Path, System.IO.FileMode.Create);
			System.IO.StreamWriter albumWriteStream = new System.IO.StreamWriter (albumStream);
			System.IO.StreamReader albumReadStream = new System.IO.StreamReader (assembly.GetManifestResourceStream (resource));
			albumWriteStream.Write (albumReadStream.ReadToEnd ());
			albumWriteStream.Close ();
		}

		private void ExportPhotos(GLib.File destBasePath)
		{
			List<ImageSize> imageSizes = new List<ImageSize> ();
			if (export_properties.RestrictHQSize) {
				imageSizes.Add (new ImageSize (export_properties.HQSize, "hq"));
			} else {
				imageSizes.Add (new ImageSize (0, "hq"));
			}
			imageSizes.Add (new ImageSize (920, "mq"));
			imageSizes.Add (new ImageSize (280,  "thumbs"));

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
