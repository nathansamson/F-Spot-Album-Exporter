using Gtk;
using Gdk;
using System;
using Cairo;

namespace FSpot.Widgets {
	[Binding(Gdk.Key.Up, "Up")]
	[Binding(Gdk.Key.Down, "Down")]
	[Binding(Gdk.Key.Left, "TiltImage", 0.05)]
	[Binding(Gdk.Key.Right, "TiltImage", -0.05)] 
	[Binding(Gdk.Key.space, "Pan")]
	public class ImageDisplay : Gtk.EventBox {
		ImageInfo current;
		ImageInfo next;
		BrowsablePointer item;
		ITransition transition;
		IEffect effect;
		Delay delay;
		int index = 0;

		ITransition Transition {
			get { return transition; }
			set { 
				if (transition != null) 
					transition.Dispose ();

				transition = value;

				if (transition != null)
					delay.Start ();
				else 
					delay.Stop ();
			}
		}

		public ImageDisplay (BrowsablePointer item) 
		{
			this.item = item;
			CanFocus = true;
			current = new ImageInfo (item.Current.DefaultVersionUri);
			if (item.Collection.Count > item.Index + 1) {
				next = new ImageInfo (item.Collection [item.Index + 1].DefaultVersionUri);
			}
			delay = new Delay (20, new GLib.IdleHandler (DrawFrame));
		}

		protected override void OnDestroyed ()
		{
			if (current != null) {
				current.Dispose ();
				current = null;
			}

		        if (next != null) {
				next.Dispose ();
				next = null;
			}
			Transition = null;
			
			if (effect != null)
				effect.Dispose ();
			
			base.OnDestroyed ();
		}
	
		public bool Up ()
		{
			Console.WriteLine ("Up");
			Transition = new CrossFade (current, next);
			return true;
		}

		public bool Down ()
		{
			Console.WriteLine ("down");
			Transition = new CrossFade (next, current);
			return true;
		}

		public bool TiltImage (double radians)
		{
			Tilt t = effect as Tilt;

			if (t == null) {
				t = new Tilt (current);
				effect = t;
			}

			t.Angle += radians;

			QueueDraw ();

			return true;
		}

		public bool Pan ()
		{
			Console.WriteLine ("space");
			Transition = new PanZoom (current);
			return true;
		}

		public bool DrawFrame ()
		{
			if (Transition != null)
				Transition.OnEvent (this);

			return true;
		}
		
		private static void SetClip (Graphics ctx, Gdk.Rectangle area) 
		{
			ctx.MoveTo (area.Left, area.Top);
			ctx.LineTo (area.Right, area.Top);
			ctx.LineTo (area.Right, area.Bottom);
			ctx.LineTo (area.Left, area.Bottom);
			
			ctx.ClosePath ();
			ctx.Clip ();
		}
		
		private static void SetClip (Graphics ctx, Region region)
		{
			foreach (Gdk.Rectangle area in region.GetRectangles ()) {
				ctx.MoveTo (area.Left, area.Top);
				ctx.LineTo (area.Right, area.Top);
				ctx.LineTo (area.Right, area.Bottom);
				ctx.LineTo (area.Left, area.Bottom);
					
				ctx.ClosePath ();
			}
			ctx.Clip ();
		}

		private void OnExpose (Graphics ctx, Region region)
		{
			if (Transition != null) {
				bool done = false;
				foreach (Gdk.Rectangle area in region.GetRectangles ()) {
					BlockProcessor proc = new BlockProcessor (area, 256);
					Gdk.Rectangle subarea;
					while (proc.Step (out subarea)) {
						ctx.Save ();
						SetClip (ctx, subarea);
						done = ! Transition.OnExpose (ctx, Allocation);
						ctx.Restore ();
					}
				}
				if (done) {
					System.Console.WriteLine ("frames = {0}", Transition.Frames);
					Transition = null;
				}
			} else if (effect != null) {
				foreach (Gdk.Rectangle area in region.GetRectangles ()) {
					BlockProcessor proc = new BlockProcessor (area, 256);
					Gdk.Rectangle subarea;
					while (proc.Step (out subarea)) {
						ctx.Save ();
						SetClip (ctx, subarea);
						effect.OnExpose (ctx, Allocation);
						ctx.Restore ();
					}
				}
			} else {
				ctx.Operator = Operator.Source;
				SurfacePattern p = new SurfacePattern (current.Surface);
				p.Filter = Filter.Fast;
				SetClip (ctx, region);
				ctx.Matrix = current.Fill (Allocation);

				ctx.Source = p;
				ctx.Paint ();
				p.Destroy ();
			}
		}

		protected override bool OnExposeEvent (EventExpose args)
		{
			bool double_buffer = false;
			base.OnExposeEvent (args);

			Graphics ctx = CairoUtils.CreateContext (GdkWindow);
			if (double_buffer) {
				ImageSurface cim = new ImageSurface (Format.RGB24, 
								     Allocation.Width, 
								     Allocation.Height);

				Graphics buffer = new Graphics (cim);
				OnExpose (buffer, args.Region);

				SurfacePattern sur = new SurfacePattern (cim);
				sur.Filter = Filter.Fast;
				ctx.Source = sur;
				SetClip (ctx, args.Region);

				ctx.Paint ();

				((IDisposable)buffer).Dispose ();
				((IDisposable)cim).Dispose ();
				sur.Destroy ();
			} else {
				OnExpose (ctx, args.Region);
			}

			((IDisposable)ctx).Dispose ();
			return true;
		}

		~ImageDisplay () 
		{
			Transition = null;
			current.Dispose ();
			next.Dispose ();
		}

		private interface ITransition : IDisposable {
			bool OnExpose (Graphics ctx, Gdk.Rectangle allocation);
			bool OnEvent (Widget w);
			int Frames { get; }
		}

		private interface IEffect : IDisposable {
			bool OnExpose (Graphics ctx, Gdk.Rectangle allocation);
		}

		private class Tilt : IEffect {
			ImageInfo info;
			double angle;
			bool horizon;
			bool plumb;
			ImageInfo cache;

			public Tilt (ImageInfo info)
			{
				this.info = info;
			}

			public double Angle {
				get { return angle; }
				set { angle = Math.Max (Math.Min (value, Math.PI * .25), Math.PI * -0.25); }
			}
			
			public bool OnExpose (Graphics ctx, Gdk.Rectangle allocation)
			{
				ctx.Operator = Operator.Source;

				SurfacePattern p = new SurfacePattern (info.Surface);

				p.Filter = Filter.Fast;
				Console.WriteLine (p.Filter);
				
				Matrix m = info.Fill (allocation, angle);
				
				ctx.Matrix = m;
				ctx.Source = p;
				ctx.Paint ();
				p.Destroy ();
				
				return true;
			}

			public void Dispose ()
			{
				
			}
		}
		
		private class PanZoomOld : ITransition {
			ImageInfo info;
			ImageInfo buffer;
			TimeSpan duration = new TimeSpan (0, 0, 7);
			double pan_x;
			double pan_y;
			double x_offset;
			double y_offset;
			DateTime start;
			int frames = 0;
			double zoom;

			public PanZoomOld (ImageInfo info)
			{
				this.info = info;
			}

			public int Frames {
				get { return frames; }
			}
			
			public bool OnEvent (Widget w)
			{
				if (frames == 0) {
					start = DateTime.UtcNow;
					Gdk.Rectangle viewport = w.Allocation;

					zoom = Math.Max (viewport.Width / (double) info.Bounds.Width,
							 viewport.Height / (double) info.Bounds.Height);
					
					zoom *= 1.2;		     
					
					x_offset = (viewport.Width - info.Bounds.Width * zoom);
					y_offset = (viewport.Height - info.Bounds.Height * zoom);

					pan_x = 0;
					pan_y = 0;
					w.QueueDraw ();
				}
				frames ++;

				double percent = Math.Min ((DateTime.UtcNow - start).Ticks / (double) duration.Ticks, 1.0);

				double x = x_offset * percent;
				double y = y_offset * percent;
				
				if (w.IsRealized){
					w.GdkWindow.Scroll ((int)(x - pan_x), (int)(y - pan_y));
					pan_x = x;
					pan_y = y;
					//w.GdkWindow.ProcessUpdates (false);
				}

				return percent < 1.0;
			}

			public bool OnExpose (Graphics ctx, Gdk.Rectangle viewport)
			{
				double percent = Math.Min ((DateTime.UtcNow - start).Ticks / (double) duration.Ticks, 1.0);

				//Matrix m = info.Fill (allocation);
				Matrix m = new Matrix ();
				m.Translate (pan_x, pan_y);
				m.Scale (zoom, zoom);
				ctx.Matrix = m;
				
				SurfacePattern p = new SurfacePattern (info.Surface);
				ctx.Source = p;
				ctx.Paint ();
				p.Destroy ();

				return percent < 1.0;
			}

			public void Dispose ()
			{
				//info.Dispose ();
			}
		}

		private class PanZoom : ITransition {
			ImageInfo info;
			ImageInfo buffer;
			TimeSpan duration = new TimeSpan (0, 0, 7);
			int pan_x;
			int pan_y;
			DateTime start;
			int frames = 0;
			double zoom;

			public PanZoom (ImageInfo info)
			{
				this.info = info;
			}

			public int Frames {
				get { return frames; }
			}
			
			public bool OnEvent (Widget w)
			{
				Gdk.Rectangle viewport = w.Allocation;
				if (buffer == null) {
					double scale = Math.Max (viewport.Width / (double) info.Bounds.Width,
							 viewport.Height / (double) info.Bounds.Height);
					
					scale *= 1.2;		     
					buffer = new ImageInfo (info, w, new Gdk.Rectangle (0, 0, (int) (info.Bounds.Width * scale), (int) (info.Bounds.Height * scale)));
					start = DateTime.UtcNow;
					//w.QueueDraw ();
					zoom = 1.0;
				}

				double percent = Math.Min ((DateTime.UtcNow - start).Ticks / (double) duration.Ticks, 1.0);

				int n_x = (int) Math.Floor ((buffer.Bounds.Width - viewport.Width) * percent);
				int n_y = (int) Math.Floor ((buffer.Bounds.Height - viewport.Height) * percent);
				
				if (n_x != pan_x || n_y != pan_y) {
					//w.GdkWindow.Scroll (- (n_x - pan_x), - (n_y - pan_y));
					w.QueueDraw ();
					w.GdkWindow.ProcessUpdates (false);
					Console.WriteLine ("{0} {1} elapsed", DateTime.UtcNow, DateTime.UtcNow - start);
				}
				pan_x = n_x;
				pan_y = n_y;

				return percent < 1.0;
			}

			public bool OnExpose (Graphics ctx, Gdk.Rectangle viewport)
			{
				double percent = Math.Min ((DateTime.UtcNow - start).Ticks / (double) duration.Ticks, 1.0);
				frames ++;

				//ctx.Matrix = m;
				
				SurfacePattern p = new SurfacePattern (buffer.Surface);
				p.Filter = Filter.Fast;
				Matrix m = new Matrix ();
				m.Translate (pan_x * zoom, pan_y * zoom);
				m.Scale (zoom, zoom);
				zoom *= .98;
				p.Matrix = m;
				ctx.Source = p;
				ctx.Paint ();
				p.Destroy ();

				return percent < 1.0;
			}

			public void Dispose ()
			{
				buffer.Dispose ();
			}
		}

		private class Slide : ITransition {
			ImageInfo begin;
			ImageInfo end;
			DateTime start;
			TimeSpan duration = new TimeSpan (0, 0, 2);
			int frames;

			public Slide (ImageInfo begin, ImageInfo end)
			{
				start = DateTime.UtcNow;
				this.begin = begin;
				this.end = end;
			}
			
			public int Frames {
				get { return frames; }
			}

			public bool OnEvent (Widget w)
			{
				TimeSpan elapsed = DateTime.UtcNow - start;
				double fraction = elapsed.Ticks / (double) duration.Ticks; 
				
				return fraction <= 1.0;
			}

			public bool OnExpose (Graphics ctx, Gdk.Rectangle allocation)
			{
				TimeSpan elapsed = DateTime.UtcNow - start;
				double fraction = elapsed.Ticks / (double) duration.Ticks; 
				
				frames++;

				ctx.Matrix.InitIdentity ();
				ctx.Operator = Operator.Source;
				
				SurfacePattern p = new SurfacePattern (begin.Surface);
				//p.Filter = Filter.Fast;
				ctx.Matrix = begin.Fill (allocation);
				ctx.Source = p;
				ctx.Paint ();

				ctx.Operator = Operator.Over;
				ctx.Matrix = end.Fill (allocation);
				SurfacePattern sur = new SurfacePattern (end.Surface);
				//sur.Filter = Filter.Fast;
				Pattern black = new SolidPattern (new Cairo.Color (0.0, 0.0, 0.0, fraction));
				ctx.Source = sur;
				ctx.Mask (black);

				return fraction < 1.0;
			}

			public void Dispose ()
			{
				
			}
		}

		private class CrossFade : ITransition {
			DateTime start;
			TimeSpan duration = new TimeSpan (0, 0, 1);
			ImageInfo begin;
			ImageInfo end;
			ImageInfo begin_buffer;
			ImageInfo end_buffer;
			int frames = 0;

			public CrossFade (ImageInfo begin, ImageInfo end)
			{
				this.begin = begin;
				this.end = end;
			}
			
			public int Frames {
				get { return frames; }
			}

			public bool OnEvent (Widget w)
			{
				if (begin_buffer == null) {
					begin_buffer = new ImageInfo (begin, w); //.Allocation);
				}

				if (end_buffer == null) {
					end_buffer = new ImageInfo (end, w); //.Allocation);
				}

				w.QueueDraw ();
				w.GdkWindow.ProcessUpdates (false);

				TimeSpan elapsed = DateTime.UtcNow - start;
				double fraction = elapsed.Ticks / (double) duration.Ticks; 

				return fraction < 1.0;
			}

			public bool OnExpose (Graphics ctx, Gdk.Rectangle allocation)
			{
				if (frames == 0)
					start = DateTime.UtcNow;

				frames ++;
				TimeSpan elapsed = DateTime.UtcNow - start;
				double fraction = elapsed.Ticks / (double) duration.Ticks; 
				double opacity = Math.Sin (Math.Min (fraction, 1.0) * Math.PI * 0.5);

				ctx.Operator = Operator.Source;
				
				SurfacePattern p = new SurfacePattern (begin_buffer.Surface);
				ctx.Matrix = begin_buffer.Fill (allocation);
				p.Filter = Filter.Fast;
				ctx.Source = p;
				ctx.Paint ();
				
				ctx.Operator = Operator.Over;
				ctx.Matrix = end_buffer.Fill (allocation);
				SurfacePattern sur = new SurfacePattern (end_buffer.Surface);
				Pattern black = new SolidPattern (new Cairo.Color (0.0, 0.0, 0.0, opacity));
				//ctx.Source = black;
				//ctx.Fill ();
				sur.Filter = Filter.Fast;
				ctx.Source = sur;
				ctx.Mask (black);
				//ctx.Paint ();

				ctx.Matrix = new Matrix ();
				
				ctx.MoveTo (allocation.Width / 2.0, allocation.Height / 2.0);
				ctx.Source = new SolidPattern (1.0, 0, 0);	
#if debug
				ctx.ShowText (String.Format ("{0} {1} {2} {3} {4} {5} {6} {7}", 
							     frames,
							     sur.Status,
							     p.Status,
							     opacity, fraction, elapsed, start, DateTime.UtcNow));
#endif
				sur.Destroy ();
				p.Destroy ();
				return fraction < 1.0;
			}
			
			public void Dispose ()
			{
				if (begin_buffer != null) {
					begin_buffer.Dispose ();
					begin_buffer = null;
				}
				
				if (end_buffer != null) {
					end_buffer.Dispose ();
					end_buffer = null;
				}
			}
		}

		private class ImageInfo : IDisposable {
			public Surface Surface;
			public Gdk.Rectangle Bounds;

			public ImageInfo (Uri uri)
			{
				ImageFile img = ImageFile.Create (uri);
				Pixbuf pixbuf = img.Load ();
				Surface = CairoUtils.CreateSurface (pixbuf);
				SetPixbuf (pixbuf);
				pixbuf.Dispose ();
			}

			public ImageInfo (Pixbuf pixbuf)
			{
				SetPixbuf (pixbuf);
			}

			public ImageInfo (ImageInfo info, Widget w) : this (info, w, w.Allocation)
			{
			}

			public ImageInfo (ImageInfo info, Widget w, Gdk.Rectangle bounds)
			{
				Cairo.Surface similar = CairoUtils.CreateSurface (w.GdkWindow);
				Bounds = bounds;
				Surface = similar.CreateSimilar (Content.ColorAlpha, Bounds.Width, Bounds.Height);
				Graphics ctx = new Graphics (Surface);
				
				ctx.Matrix = info.Fill (Bounds);
				Pattern p = new SurfacePattern (info.Surface);
				ctx.Source = p;
				ctx.Paint ();
				((IDisposable)ctx).Dispose ();
				p.Destroy ();
			}

			public ImageInfo (ImageInfo info, Gdk.Rectangle allocation)
			{
#if false
				Surface = new ImageSurface (Format.RGB24,
							    allocation.Width,
							    allocation.Height);
				Graphics ctx = new Graphics (Surface);
#else
				Console.WriteLine ("source status = {0}", info.Surface.Status);
				Surface = info.Surface.CreateSimilar (Content.Color,
								      allocation.Width,
								      allocation.Height);
				
				System.Console.WriteLine ("status = {1} pointer = {0}", Surface.Pointer.ToString (), Surface.Status);
				Graphics ctx = new Graphics (Surface);
#endif
				Bounds = allocation;
				
				ctx.Matrix = info.Fill (allocation);
				Pattern p = new SurfacePattern (info.Surface);
				ctx.Source = p;
				ctx.Paint ();
				((IDisposable)ctx).Dispose ();
				p.Destroy ();
			}

			private void SetPixbuf (Pixbuf pixbuf)
			{
				Surface = CairoUtils.CreateSurface (pixbuf);
				Bounds.Width = pixbuf.Width;
				Bounds.Height = pixbuf.Height;
			}

			public Matrix Fill (Gdk.Rectangle viewport) 
			{
				Matrix m = new Matrix ();
				m.InitIdentity ();
				
				double scale = Math.Max (viewport.Width / (double) Bounds.Width,
							 viewport.Height / (double) Bounds.Height);
				
				double x_offset = (viewport.Width  - Bounds.Width * scale) / 2.0;
				double y_offset = (viewport.Height  - Bounds.Height * scale) / 2.0;
				
				m.Translate (x_offset, y_offset);
				m.Scale (scale, scale);
				return m;
			}

			//
			// this functions calculates the transformation needed to center and completely fill the
			// viewport with the Surface at the given tilt
			//
			public Matrix Fill (Gdk.Rectangle viewport, double tilt)
			{
				if (tilt == 0.0)
					return Fill (viewport);

				Matrix m = new Matrix ();
				m.InitIdentity ();

				double len;
				double orig_len;
				if (Bounds.Width > Bounds.Height) {
					len = viewport.Height;
					orig_len = Bounds.Height;
				} else {
					len = viewport.Width;
					orig_len = Bounds.Width;
				}

				double a = Math.Sqrt (viewport.Width * viewport.Width + viewport.Height * viewport.Height);
				double alpha = Math.Acos (len / a);
				double theta = alpha - Math.Abs (tilt);
				
				double slen = a * Math.Cos (theta);
				
				double scale = slen / orig_len;
				
				double x_offset = (viewport.Width  - Bounds.Width * scale) / 2.0;
				double y_offset = (viewport.Height  - Bounds.Height * scale) / 2.0;

				m.Translate (x_offset, y_offset);
				m.Scale (scale, scale);
				m.Invert ();
				m.Translate (viewport.Width * 0.5, viewport.Height * 0.5);
				m.Rotate (tilt);
				m.Translate (viewport.Width * -0.5, viewport.Height * -0.5);
				m.Invert ();
				return m;
			}

			public Matrix Fit (Gdk.Rectangle viewport)
			{
				Matrix m = new Matrix ();
				m.InitIdentity ();
				
				double scale = Math.Min (viewport.Width / (double) Bounds.Width,
							 viewport.Height / (double) Bounds.Height);
				
				double x_offset = (viewport.Width  - Bounds.Width * scale) / 2.0;
				double y_offset = (viewport.Height  - Bounds.Height * scale) / 2.0;
				
				m.Translate (x_offset, y_offset);
				m.Scale (scale, scale);
				return m;
			}

			public void Dispose ()
			{
				((IDisposable)Surface).Dispose ();
			}
		}
	}
}

