//
// InfoBox.cs
//
// Author:
//   Ruben Vermeersch <ruben@savanne.be>
//   Stephane Delcroix <sdelcroix@novell.com>
//   Mike Gemuende <mike@gemuende.de>
//
// Copyright (C) 2008-2010 Novell, Inc.
// Copyright (C) 2008, 2010 Ruben Vermeersch
// Copyright (C) 2008 Stephane Delcroix
// Copyright (C) 2010 Mike Gemuende
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

using Gtk;
using System;
using System.Collections.Generic;
using System.IO;
using FSpot.Core;
using FSpot.Imaging;
using Mono.Unix;
using FSpot.Utils;
using GLib;
using GFile = GLib.File;
using GFileInfo = GLib.FileInfo;
using Hyena;

// FIXME TODO: We want to use something like EClippedLabel here throughout so it handles small sizes
// gracefully using ellipsis.

namespace FSpot.Widgets
{
    public class InfoBox : VBox
    {
        DelayedOperation update_delay;

        public struct InfoEntry
        {
            public bool TwoColumns;
            public bool AlwaysVisible;
            public bool DefaultVisibility;
            public string Id;
            public string Description;
            public Widget LabelWidget;
            public Widget InfoWidget;
            public Action<Widget, IPhoto, TagLib.Image.File> SetSingle;
            public Action<Widget, IPhoto[]> SetMultiple;
        }

        private List<InfoEntry> entries = new List<InfoEntry> ();

        private void AddEntry (string id, string name, string description, Widget info_widget, float label_y_align,
                               bool default_visibility,
                               Action<Widget, IPhoto, TagLib.Image.File> set_single,
                               Action<Widget, IPhoto[]> set_multiple)
        {
            entries.Add (new InfoEntry {
                TwoColumns = (name == null),
                AlwaysVisible = (id == null) || (description == null),
                DefaultVisibility = default_visibility,
                Id = id,
                Description = description,
                LabelWidget = CreateRightAlignedLabel (String.Format ("<b>{0}</b>", name), label_y_align),
                InfoWidget = info_widget,
                SetSingle = set_single,
                SetMultiple = set_multiple
            });
        }

        private void AddEntry (string id, string name, string description, Widget info_widget, float label_y_align,
                               Action<Widget, IPhoto, TagLib.Image.File> set_single,
                               Action<Widget, IPhoto[]> set_multiple)
        {
            AddEntry (id, name, description, info_widget, label_y_align, true, set_single, set_multiple);
        }

        private void AddEntry (string id, string name, string description, Widget info_widget, bool default_visibility,
                               Action<Widget, IPhoto, TagLib.Image.File> set_single,
                               Action<Widget, IPhoto[]> set_multiple)
        {
            AddEntry (id, name, description, info_widget, 0.0f, default_visibility, set_single, set_multiple);
        }

        private void AddEntry (string id, string name, string description, Widget info_widget,
                               Action<Widget, IPhoto, TagLib.Image.File> set_single,
                               Action<Widget, IPhoto[]> set_multiple)
        {
            AddEntry (id, name, description, info_widget, 0.0f, set_single, set_multiple);
        }

        private void AddLabelEntry (string id, string name, string description,
                                    Func<IPhoto, TagLib.Image.File, string> single_string,
                                    Func<IPhoto[], string> multiple_string)
        {
            AddLabelEntry (id, name, description, true, single_string, multiple_string);
        }

        private void AddLabelEntry (string id, string name, string description, bool default_visibility,
                                    Func<IPhoto, TagLib.Image.File, string> single_string,
                                    Func<IPhoto[], string> multiple_string)
        {
            Action<Widget, IPhoto, TagLib.Image.File> set_single = (widget, photo, metadata) => {
                if (metadata != null)
                    (widget as Label).Text = single_string (photo, metadata);
                else
                    (widget as Label).Text = Catalog.GetString ("(Unknown)");
            };
            
            Action<Widget, IPhoto[]> set_multiple = (widget, photos) => {
                (widget as Label).Text = multiple_string (photos);
            };
            
            AddEntry (id, name, description, CreateLeftAlignedLabel (String.Empty), default_visibility,
                      single_string == null ? null : set_single,
                      multiple_string == null ? null : set_multiple);
        }


        private IPhoto[] photos = new IPhoto[0];
        public IPhoto[] Photos {
            private get { return photos; }
            set {
                photos = value;
                update_delay.Start ();
            }
        }

        public IPhoto Photo {
            set {
                if (value != null) {
                    Photos = new IPhoto[] { value };
                }
            }
        }

        private bool show_tags = false;
        public bool ShowTags {
            get { return show_tags; }
            set {
                if (show_tags == value)
                    return;
                
                show_tags = value;
                //      tag_view.Visible = show_tags;
            }
        }

        private bool show_rating = false;
        public bool ShowRating {
            get { return show_rating; }
            set {
                if (show_rating == value)
                    return;
                
                show_rating = value;
                //      rating_label.Visible = show_rating;
                //      rating_view.Visible = show_rating;
            }
        }

        public delegate void VersionChangedHandler (InfoBox info_box, IPhotoVersion version);
        public event VersionChangedHandler VersionChanged;

        private Expander info_expander;
        private Expander histogram_expander;

        private Gtk.Image histogram_image;
        private Histogram histogram;

        private DelayedOperation histogram_delay;

        // Context switching (toggles visibility).
        public event EventHandler ContextChanged;

        private ViewContext view_context = ViewContext.Unknown;
        public ViewContext Context {
            get { return view_context; }
            set {
                view_context = value;
                if (ContextChanged != null)
                    ContextChanged (this, null);
            }
        }

        private readonly InfoBoxContextSwitchStrategy ContextSwitchStrategy;

        // Widgetry.
        private ListStore version_list;
        private ComboBox version_combo;


        private void HandleRatingChanged (object o, EventArgs e)
        {
            App.Instance.Organizer.HandleRatingMenuSelected ((o as Widgets.RatingEntry).Value);
        }

        private Label CreateRightAlignedLabel (string text, float yalign)
        {
            Label label = new Label ();
            label.UseMarkup = true;
            label.Markup = text;
            label.Xalign = 1.0f;
            label.Yalign = yalign;
            
            return label;
        }

        private Label CreateLeftAlignedLabel (string text)
        {
            Label label = new Label ();
            label.UseMarkup = true;
            label.Markup = text;
            label.Xalign = 0.0f;
            label.Yalign = 0.0f;
            label.Selectable = true;
            label.Ellipsize = Pango.EllipsizeMode.End;
            
            return label;
        }

        private Table info_table;

        private void AttachRow (int row, InfoEntry entry)
        {
            if (!entry.TwoColumns) {
                info_table.Attach (entry.LabelWidget, 0, 1, (uint)row, (uint)row + 1, AttachOptions.Fill, AttachOptions.Fill, TABLE_XPADDING, TABLE_YPADDING);
            }
            
            info_table.Attach (entry.InfoWidget, entry.TwoColumns ? 0u : 1u, 2, (uint)row, (uint)row + 1, AttachOptions.Expand | AttachOptions.Fill, AttachOptions.Expand | AttachOptions.Fill, TABLE_XPADDING, TABLE_YPADDING);
            
            var info_label = entry.InfoWidget as Label;
            if (info_label != null)
                info_label.PopulatePopup += HandlePopulatePopup;
            
            var info_entry = entry.InfoWidget as Entry;
            if (info_entry != null)
                info_entry.PopulatePopup += HandlePopulatePopup;
            ;
        }

        private void UpdateTable ()
        {
            info_table.Resize ((uint)(head_rows + entries.Count), 2);
            int i = 0;
            foreach (var entry in entries) {
                AttachRow (head_rows + i, entry);
                i++;
            }
        }


        private void SetEntryWidgetVisibility (InfoEntry entry, bool def)
        {
            entry.InfoWidget.Visible = ContextSwitchStrategy.InfoEntryVisible (Context, entry) && def;
            entry.LabelWidget.Visible = ContextSwitchStrategy.InfoEntryVisible (Context, entry) && def;
            
        }

        private void UpdateEntries ()
        {
            
        }

        const int TABLE_XPADDING = 3;
        const int TABLE_YPADDING = 3;
        private Label AttachLabel (Table table, int row_num, Widget entry)
        {
            Label label = new Label (String.Empty);
            label.Xalign = 0;
            label.Selectable = true;
            label.Ellipsize = Pango.EllipsizeMode.End;
            label.Show ();
            
            label.PopulatePopup += HandlePopulatePopup;
            
            table.Attach (label, 1, 2, (uint)row_num, (uint)row_num + 1, AttachOptions.Expand | AttachOptions.Fill, AttachOptions.Expand | AttachOptions.Fill, (uint)entry.Style.XThickness + TABLE_XPADDING, (uint)entry.Style.YThickness);
            
            return label;
        }

        private const int head_rows = 0;

        private void SetupWidgets ()
        {
            
            histogram_expander = new Expander (Catalog.GetString ("Histogram"));
            histogram_expander.Activated += delegate(object sender, EventArgs e) {
                ContextSwitchStrategy.SetHistogramVisible (Context, histogram_expander.Expanded);
                UpdateHistogram ();
            };
            histogram_expander.StyleSet += delegate(object sender, StyleSetArgs args) {
                Gdk.Color c = this.Toplevel.Style.Backgrounds[(int)Gtk.StateType.Active];
                histogram.RedColorHint = (byte)(c.Red / 0xff);
                histogram.GreenColorHint = (byte)(c.Green / 0xff);
                histogram.BlueColorHint = (byte)(c.Blue / 0xff);
                histogram.BackgroundColorHint = 0xff;
                UpdateHistogram ();
            };
            histogram_image = new Gtk.Image ();
            histogram = new Histogram ();
            histogram_expander.Add (histogram_image);
            
            Add (histogram_expander);
            
            info_expander = new Expander (Catalog.GetString ("Image Information"));
            info_expander.Activated += (sender, e) => {
                ContextSwitchStrategy.SetInfoBoxVisible (Context, info_expander.Expanded);
            };

            info_table = new Table (head_rows, 2, false) { BorderWidth = 0 };
            
            AddLabelEntry (null, null, null, null,
                           photos => { return String.Format (Catalog.GetString ("{0} Photos"), photos.Length); });
            
            AddLabelEntry (null, Catalog.GetString ("Name"), null,
                           (photo, file) => { return photo.Name ?? String.Empty; }, null);
            
            version_list = new ListStore (typeof(IPhotoVersion), typeof(string), typeof(bool));
            version_combo = new ComboBox ();
            CellRendererText version_name_cell = new CellRendererText ();
            version_name_cell.Ellipsize = Pango.EllipsizeMode.End;
            version_combo.PackStart (version_name_cell, true);
            version_combo.SetCellDataFunc (version_name_cell, new CellLayoutDataFunc (VersionNameCellFunc));
            version_combo.Model = version_list;
            version_combo.Changed += OnVersionComboChanged;
            
            AddEntry (null, Catalog.GetString ("Version"), null, version_combo, 0.5f,
                      (widget, photo, file) => {
                            version_list.Clear ();
                            version_combo.Changed -= OnVersionComboChanged;
            
                            int count = 0;
                            foreach (IPhotoVersion version in photo.Versions) {
                                version_list.AppendValues (version, version.Name, true);
                                if (version == photo.DefaultVersion)
                                    version_combo.Active = count;
                                count++;
                            }
            
                            if (count <= 1) {
                                version_combo.Sensitive = false;
                                version_combo.TooltipText = Catalog.GetString ("(No Edits)");
                            } else {
                                version_combo.Sensitive = true;
                                version_combo.TooltipText =
                                    String.Format (Catalog.GetPluralString ("(One Edit)", "({0} Edits)", count - 1),
                                                   count - 1);
                            }
                            version_combo.Changed += OnVersionComboChanged;
                       }, null);
            
            AddLabelEntry ("date", Catalog.GetString ("Date"), Catalog.GetString ("Show Date"),
                           (photo, file) => {
                               return String.Format ("{0}{2}{1}",
                                                     photo.Time.ToShortDateString (),
                                                     photo.Time.ToShortTimeString (),
                                                     Environment.NewLine); },
                           photos => {
                                IPhoto first = photos[photos.Length - 1];
                                IPhoto last = photos[0];
                                if (first.Time.Date == last.Time.Date) {
                                    //Note for translators: {0} is a date, {1} and {2} are times.
                                    return String.Format (Catalog.GetString ("On {0} between \n{1} and {2}"),
                                                          first.Time.ToShortDateString (),
                                                          first.Time.ToShortTimeString (),
                                                          last.Time.ToShortTimeString ());
                                } else {
                                    return String.Format (Catalog.GetString ("Between {0} \nand {1}"),
                                                          first.Time.ToShortDateString (),
                                                          last.Time.ToShortDateString ());
                                }
                           });

            AddLabelEntry ("size", Catalog.GetString ("Size"), Catalog.GetString ("Show Size"),
                           (photo, metadata) => {
                                int width = metadata.Properties.PhotoWidth;
                                int height = metadata.Properties.PhotoHeight;
                
                                if (width != 0 && height != 0)
                                    return String.Format ("{0}x{1}", width, height);
                                else
                                    return Catalog.GetString ("(Unknown)");
                           }, null);

            AddLabelEntry ("exposure", Catalog.GetString ("Exposure"), Catalog.GetString ("Show Exposure"),
                           (photo, metadata) => {
                                var fnumber = metadata.ImageTag.FNumber;
                                var exposure_time = metadata.ImageTag.ExposureTime;
                                var iso_speed = metadata.ImageTag.ISOSpeedRatings;

                                string info = String.Empty;

                                if (fnumber.HasValue && fnumber.Value != 0.0) {
                                    info += String.Format ("f/{0:.0} ", fnumber.Value);
                                }

                                if (exposure_time.HasValue) {
                                    if (Math.Abs (exposure_time.Value) >= 1.0) {
                                        info += String.Format ("{0} sec ", exposure_time.Value);
                                    } else {
                                        info += String.Format ("1/{0} sec ", (int)(1 / exposure_time.Value));
                                    }
                                }

                                if (iso_speed.HasValue) {
                                    info += String.Format ("{0}ISO {1}", Environment.NewLine, iso_speed.Value);
                                }

                                var exif = metadata.ImageTag.Exif;
                                if (exif != null) {
                                    var flash = exif.ExifIFD.GetLongValue (0, (ushort)TagLib.IFD.Tags.ExifEntryTag.Flash);

                                    if (flash.HasValue) {
                                        if ((flash.Value & 0x01) == 0x01)
                                            info += String.Format (", {0}", Catalog.GetString ("flash fired"));
                                        else
                                            info += String.Format (", {0}", Catalog.GetString ("flash didn't fire"));
                                    }
                                }

                                if (info == String.Empty)
                                    return Catalog.GetString ("(None)");

                                return info;
                           }, null);
            
            AddLabelEntry ("focal_length", Catalog.GetString ("Focal Length"), Catalog.GetString ("Show Focal Length"),
                           false, (photo, metadata) => {
                                var focal_length = metadata.ImageTag.FocalLength;
                
                                if (focal_length == null)
                                    return Catalog.GetString ("(Unknown)");
                                else
                                    return String.Format ("{0} mm", focal_length.Value);
                            }, null);

            AddLabelEntry ("camera", Catalog.GetString ("Camera"), Catalog.GetString ("Show Camera"), false,
                           (photo, metadata) => { return metadata.ImageTag.Model ?? Catalog.GetString ("(Unknown)"); },
                           null);
            
            AddLabelEntry ("creator", Catalog.GetString ("Creator"), Catalog.GetString ("Show Creator"),
                           (photo, metadata) => { return metadata.ImageTag.Creator ?? Catalog.GetString ("(Unknown)"); },
                           null);

            AddLabelEntry ("file_size", Catalog.GetString ("File Size"), Catalog.GetString ("Show File Size"), false,
                           (photo, metadata) => {
                                try {
                                    GFile file = FileFactory.NewForUri (photo.DefaultVersion.Uri);
                                    GFileInfo file_info = file.QueryInfo ("standard::size", FileQueryInfoFlags.None, null);
                                    return Format.SizeForDisplay (file_info.Size);
                                } catch (GLib.GException e) {
                                    Hyena.Log.DebugException (e);
                                    return Catalog.GetString ("(File read error)");
                                }
                            }, null);
            
            var rating_entry = new RatingEntry { HasFrame = false, AlwaysShowEmptyStars = true };
            rating_entry.Changed += HandleRatingChanged;
            var rating_align = new Gtk.Alignment (0, 0, 0, 0);
            rating_align.Add (rating_entry);
            AddEntry ("rating", Catalog.GetString ("Rating"), Catalog.GetString ("Show Rating"), rating_align, false,
                      (widget, photo, metadata) => { ((widget as Alignment).Child as RatingEntry).Value = (int) photo.Rating; },
                      null);
            
            AddEntry ("tag", null, Catalog.GetString ("Show Tags"), new TagView (), false,
                      (widget, photo, metadata) => { (widget as TagView).Current = photo; }, null);

            UpdateTable ();

            EventBox eb = new EventBox ();
            eb.Add (info_table);
            info_expander.Add (eb);
            eb.ButtonPressEvent += HandleButtonPressEvent;

            Add (info_expander);
        }

        public bool Update ()
        {
            if (Photos == null || Photos.Length == 0) {
                Hide ();
            } else if (Photos.Length == 1) {
                var photo = Photos[0];

                histogram_expander.Visible = true;
                UpdateHistogram();

                using (var metadata = Metadata.Parse (photo.DefaultVersion.Uri)) {
                    foreach (var entry in entries) {
                        bool is_single = (entry.SetSingle != null);
                        
                        if (is_single)
                            entry.SetSingle (entry.InfoWidget, photo, metadata);

                        SetEntryWidgetVisibility (entry, is_single);
                    }
                }
                Show ();
            } else if (Photos.Length > 1) {
                foreach (var entry in entries) {
                    bool is_multiple = (entry.SetMultiple != null);
                    
                    if (is_multiple)
                        entry.SetMultiple (entry.InfoWidget, Photos);
                    
                    SetEntryWidgetVisibility (entry, is_multiple);
                }
                histogram_expander.Visible = false;
                Show ();
            }
            return false;
        }

        void VersionNameCellFunc (CellLayout cell_layout, CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            string name = (string)tree_model.GetValue (iter, 1);
            (cell as CellRendererText).Text = name;
            
            cell.Sensitive = (bool)tree_model.GetValue (iter, 2);
        }

        void OnVersionComboChanged (object o, EventArgs e)
        {
            ComboBox combo = o as ComboBox;
            if (combo == null)
                return;
            
            TreeIter iter;
            
            if (combo.GetActiveIter (out iter))
                VersionChanged (this, (IPhotoVersion)version_list.GetValue (iter, 0));
        }

        private Gdk.Pixbuf histogram_hint;

        private void UpdateHistogram ()
        {
            if (histogram_expander.Expanded)
                histogram_delay.Start ();
        }

        public void UpdateHistogram (Gdk.Pixbuf pixbuf)
        {
            histogram_hint = pixbuf;
            UpdateHistogram ();
        }

        private bool DelayedUpdateHistogram ()
        {
            if (Photos.Length == 0)
                return false;

            IPhoto photo = Photos[0];
            
            Gdk.Pixbuf hint = histogram_hint;
            histogram_hint = null;
            int max = histogram_expander.Allocation.Width;
            
            try {
                if (hint == null)
                    using (var img = ImageFile.Create (photo.DefaultVersion.Uri)) {
                        hint = img.Load (256, 256);
                    }
                
                histogram_image.Pixbuf = histogram.Generate (hint, max);
                
                hint.Dispose ();
            } catch (System.Exception e) {
                Hyena.Log.Debug (e.StackTrace);
                using (Gdk.Pixbuf empty = new Gdk.Pixbuf (Gdk.Colorspace.Rgb, true, 8, 256, 256)) {
                    empty.Fill (0x0);
                    histogram_image.Pixbuf = histogram.Generate (empty, max);
                }
            }
            
            return false;
        }

        // Context switching

        private void HandleContextChanged (object sender, EventArgs args)
        {
            bool infobox_visible = ContextSwitchStrategy.InfoBoxVisible (Context);
            info_expander.Expanded = infobox_visible;
            
            bool histogram_visible = ContextSwitchStrategy.HistogramVisible (Context);
            histogram_expander.Expanded = histogram_visible;
            
            if (infobox_visible)
                update_delay.Start ();
        }

        public void HandleMainWindowViewModeChanged (object o, EventArgs args)
        {
            MainWindow.ModeType mode = App.Instance.Organizer.ViewMode;
            if (mode == MainWindow.ModeType.IconView)
                Context = ViewContext.Library; else if (mode == MainWindow.ModeType.PhotoView) {
                Context = ViewContext.Edit;
            }
        }

        void HandleButtonPressEvent (object sender, ButtonPressEventArgs args)
        {
            if (args.Event.Button == 3) {
                Menu popup_menu = new Menu ();
                
                AddMenuItems (popup_menu);
                
                if (args.Event != null)
                    popup_menu.Popup (null, null, null, args.Event.Button, args.Event.Time);
                else
                    popup_menu.Popup (null, null, null, 0, Gtk.Global.CurrentEventTime);
                
                args.RetVal = true;
            }
        }

        void HandlePopulatePopup (object sender, PopulatePopupArgs args)
        {
            AddMenuItems (args.Menu);
            
            args.RetVal = true;
        }

        private void AddMenuItems (Menu popup_menu)
        {
            var items = new Dictionary <MenuItem, InfoEntry> ();

            if (popup_menu.Children.Length > 0 && entries.Count > 0) {
                GtkUtil.MakeMenuSeparator (popup_menu);
            }
            
            foreach (var entry in entries) {
                if (entry.AlwaysVisible)
                    continue;

                var item =
                    GtkUtil.MakeCheckMenuItem (popup_menu, entry.Description, (sender, args) => {
                        ContextSwitchStrategy.SetInfoEntryVisible (Context, items [sender as CheckMenuItem], (sender as CheckMenuItem).Active);
                        Update ();
                    },
                    true, ContextSwitchStrategy.InfoEntryVisible (Context, entry), false);

                items.Add (item, entry);
            }
        }

        private void HandleMenuItemSelected (object sender, EventArgs args)
        {

        }

        // Constructor.

        public InfoBox () : base(false, 0)
        {
            ContextSwitchStrategy = new MRUInfoBoxContextSwitchStrategy ();
            ContextChanged += HandleContextChanged;
            
            SetupWidgets ();
            
            update_delay = new DelayedOperation (Update);
            update_delay.Start ();
            
            histogram_delay = new DelayedOperation (DelayedUpdateHistogram);
            
            BorderWidth = 2;
            Hide ();
        }
    }

    // Decides whether infobox / histogram should be shown for each context. Implemented
    // using the Strategy pattern, to make it swappable easily, in case the
    // default MRUInfoBoxContextSwitchStrategy is not sufficiently usable.
    public abstract class InfoBoxContextSwitchStrategy
    {
        public abstract bool InfoBoxVisible (ViewContext context);
        public abstract bool HistogramVisible (ViewContext context);

        public abstract bool InfoEntryVisible (ViewContext context, InfoBox.InfoEntry entry);

        public abstract void SetInfoBoxVisible (ViewContext context, bool visible);
        public abstract void SetHistogramVisible (ViewContext context, bool visible);

        public abstract void SetInfoEntryVisible (ViewContext context, InfoBox.InfoEntry entry, bool visible);
    }

    // Values are stored as strings, because bool is not nullable through Preferences.
    public class MRUInfoBoxContextSwitchStrategy : InfoBoxContextSwitchStrategy
    {
        public const string PREF_PREFIX = Preferences.APP_FSPOT + "ui";

        private string PrefKeyForContext (ViewContext context, string item)
        {
            return String.Format ("{0}/{1}_visible/{2}", PREF_PREFIX, item, context);
        }

        private string PrefKeyForContext (ViewContext context, string parent, string item)
        {
            return String.Format ("{0}/{1}_visible/{2}/{3}", PREF_PREFIX, parent, item, context);
        }

        private bool VisibilityForContext (ViewContext context, string item, bool default_value)
        {
            string visible = Preferences.Get<string> (PrefKeyForContext (context, item));
            if (visible == null)
                return default_value;
            else
                return visible == "1";
        }

        private bool VisibilityForContext (ViewContext context, string parent, string item, bool default_value)
        {
            string visible = Preferences.Get<string> (PrefKeyForContext (context, parent, item));
            if (visible == null)
                return default_value;
            else
                return visible == "1";
        }

        private void SetVisibilityForContext (ViewContext context, string item, bool visible)
        {
            Preferences.Set (PrefKeyForContext (context, item), visible ? "1" : "0");
        }

        private void SetVisibilityForContext (ViewContext context, string parent, string item, bool visible)
        {
            Preferences.Set (PrefKeyForContext (context, parent, item), visible ? "1" : "0");
        }

        public override bool InfoBoxVisible (ViewContext context)
        {
            return VisibilityForContext (context, "infobox", true);
        }

        public override bool HistogramVisible (ViewContext context)
        {
            return VisibilityForContext (context, "histogram", true);
        }

        public override bool InfoEntryVisible (ViewContext context, InfoBox.InfoEntry entry)
        {
            if (entry.AlwaysVisible)
                return true;
            
            return VisibilityForContext (context, "infobox", entry.Id, true);
        }

        public override void SetInfoBoxVisible (ViewContext context, bool visible)
        {
            SetVisibilityForContext (context, "infobox", visible);
        }

        public override void SetHistogramVisible (ViewContext context, bool visible)
        {
            SetVisibilityForContext (context, "histogram", visible);
        }

        public override void SetInfoEntryVisible (ViewContext context, InfoBox.InfoEntry entry, bool visible)
        {
            Hyena.Log.DebugFormat ("Set Visibility for Entry {0} to {1}", entry.Id, visible);
            if (entry.AlwaysVisible)
                throw new Exception ("entry visibility cannot be set");
            
            SetVisibilityForContext (context, "infobox", entry.Id, visible);
        }
    }
}
