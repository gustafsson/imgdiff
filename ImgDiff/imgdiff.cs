using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

/**
 * A doodle, merely a work in progess. There's close to not a single best practice being followed here.
 */
namespace ImgDiff
{
	public partial class imgdiff : Window
	{
		double R2_threshold;

		FixedFileWatcher FixedFileWatcher_;
		ImageCache ImageCache_;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			this.Build ();

			MySettings settings = MySettings.load();

			GLib.ExceptionManager.UnhandledException += HandleUnhandledException;

			R2_threshold = settings.R2_Threshold;
			this.entryR2.Text = R2_threshold.ToString("G");

			ImageCache_ = new ImageCache();
			FixedFileWatcher_ = new FixedFileWatcher();
			FixedFileWatcher_.Changed += (object sender, string[] files) => {
				Gtk.Application.Invoke( delegate {
					ImageCache_.prune(files);
					Update();
				});
			};

			this.entryWatchedFolder.Changed += (sender, e) => HandleNewWatchedFolder();
			this.filechooserbutton2.CurrentFolderChanged += (object sender, EventArgs e) => {
				if ( string.IsNullOrWhiteSpace(this.entryWatchedFolder.Text)
				    || new DirectoryInfo(this.entryWatchedFolder.Text).FullName != this.filechooserbutton2.CurrentFolder)
				{
					this.entryWatchedFolder.Text = this.filechooserbutton2.CurrentFolder;
				}
			};

			this.entryWatchedFolder.Text = settings.Path;
		}



		void HandleUnhandledException (GLib.UnhandledExceptionArgs args)
		{
			Exception x = args.ExceptionObject as Exception;
			if (x != null)
				statusbartext( x.Message );
			else
				statusbartext("An unhandled exception occured. " + args.ExceptionObject.ToString());

			System.Console.WriteLine (args.ExceptionObject.ToString());
		}

		protected void OnDeleteEvent (object sender, DeleteEventArgs a)
		{
			Application.Quit ();
			a.RetVal = true;
		}

		void HandleNewWatchedFolder ()
		{
			string folder = this.entryWatchedFolder.Text;
			if (FixedFileWatcher_.Path == folder)
				return;

			FixedFileWatcher_.Path = folder;

			if (Directory.Exists (folder))
				this.filechooserbutton2.SetCurrentFolder (folder);

			Update();
		}

		void Update ()
		{
			updateSettings();
			ImageCache_.flagToPrune();

			Stopwatch watch = new Stopwatch ();
			watch.Start ();

			string[] files;
			string folder = this.entryWatchedFolder.Text;
			if (Directory.Exists (folder))
				files = Directory.GetFiles (folder);
			else
				files = new string[0];

			uint j = 0;

			Table table = new Table (Math.Max (1, 2 * (uint)files.Length), 7u, false);
			Gdk.Color col = new Gdk.Color ();
			Gdk.Color.Parse ("red", ref col);
			List<string> imagefiles = new List<string> ();
			List<double> R2_Values = new List<double> ();
			for (int i=0; i<files.Length; ++i) {
				try {
					if (ReferenceStore.equalfiles (files [i]))
					{
						imagefiles.Add (files [i]);
						R2_Values.Add (1);
						continue;
					}

					AspectFrame af = getImage (files [i]);
					AspectFrame af2 = getReferenceImage (files [i]);

					ImageCache_.updateCache (af.Child as Image);
					ImageCache_.updateCache (af2.Child as Image);

					VBox sumbox = new VBox ();

					string diffstring;
					double R2 = 0;
					AspectFrame af3 = getDiff (af, af2, out diffstring, out R2);
					if (R2 >= R2_threshold) {
						imagefiles.Add (files [i]);
						R2_Values.Add (R2);
						continue;
					}

					if (af3 != null)
						sumbox.Add (af3);

					Label infotext = new Label (diffstring);
					sumbox.Add (infotext);
					Box.BoxChild infotextbc = ((Box.BoxChild)(sumbox [infotext]));
					infotextbc.Expand = false;
					sumbox.Add (new VBox ());

					int mywidth = 24;

					Image arrow = new Image ();
					arrow.Pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-go-forward", IconSize.LargeToolbar);
					Button button = new Button ();
					button.SetSizeRequest (mywidth, 15);
					button.Add (arrow);
					button.Clicked += (object sender, EventArgs e) => validateImage (af, af2);
					sumbox.Add (button);
					sumbox.Add (new VBox ());
					Gdk.Pixbuf pixbuf;
					if (af2.Child.Data ["pixbuf"] != null)
						pixbuf = af2.Child.Data ["pixbuf"] as Gdk.Pixbuf;
					else
						pixbuf = af.Child.Data ["pixbuf"] as Gdk.Pixbuf;
					int height = pixbuf.Height;
					int width = pixbuf.Width;
					this.SizeAllocated += (o, args) => {
						int scrollbarwidth = 20;
						sumbox.SetSizeRequest (mywidth, (this.Allocation.Width - mywidth - scrollbarwidth) / 2 * height / width);
					};

					j += 2;

					Label nametext = new Label (System.IO.Path.GetFileName (files [i]));
					Label nametext2 = new Label (System.IO.Path.GetFileName (files [i]));
					table.Attach (nametext, 0u, 3u, j - 2, j - 1);
					table.Attach (nametext2, 4u, 7u, j - 2, j - 1);
					table.Attach (af, 0u, 3u, j - 1, j);
					table.Attach (sumbox, 3u, 4u, j - 1, j);
					table.Attach (af2, 4u, 7u, j - 1, j);
				} catch (GLib.GException x) {
					if (x.Source == "gdk-sharp") {
						// Found a file that wasn't an image
					} else {
						System.Console.WriteLine (x.Message);
					}
				} catch (Exception x) {
					if (x.Source == "gdk-sharp") {
						// Found a file that wasn't an image
					} else {
						System.Console.WriteLine (x.Message);
					}
				}
			}

			if (j < table.NRows) {
				//this.table1.Attach (null, 0u, 3u, j, this.table1.NRows);
				table.Resize (Math.Max (1, j), table.NColumns);
			}

			if (0 == j) {
				if (R2_Values.Count > 0) {
					table.Attach (new Label ("All images were compared within the threshold to the stored references files!"),
					              0, 7, 0, 1);

					int noneidentical_thresholded = 0;
					R2_Values.ForEach (x => noneidentical_thresholded += x < 1 ? 1 : 0 );
					if (noneidentical_thresholded > 0)
						statusbartext (string.Format ("OK ({0} image{1} not identical but within the threshold)",
						                            noneidentical_thresholded,
						                            noneidentical_thresholded == 1 ? " was" : "s were"));
					else
						statusbartext ("OK");
				} else if (!Directory.Exists (folder)) {
					statusbartext ("Folder " + folder + " does not exist.");
				} else
					statusbartext ("Found no image files in folder " + folder + ".");
			} else
				statusbartext (string.Format ("{0} image{1} does not match reference image{1}", j / 2, j == 2 ? "" : "s"));

			if (scrolledwindow1.Child != null) {
				Bin c = (scrolledwindow1.Child as Bin);
				if (null != c.Child)
					c.Remove (c.Child);
			}

			VBox vbox = new VBox ();
			scrolledwindow1.AddWithViewport (vbox);

			ImageCache_.prune();

			watch.Stop ();
			System.Console.WriteLine (string.Format ("Updated {1} files in {0} s", watch.ElapsedMilliseconds * 1e-3, j/2));

			vbox.Add (table);
			Box.BoxChild bc = ((Box.BoxChild)(vbox [table]));
			bc.Expand = false;

			if (R2_Values.Count > 0) {
				Table sumtable = new Table(1, 7, false);
				sumtable.Attach (new Label ("Files within threshold\n" 
				                         + String.Join ("\n", imagefiles.ConvertAll (x => System.IO.Path.GetFileName (x)))),
				              0, 6, j, j + 1);
				sumtable.Attach (new Label ("R2\n" 
				                         + String.Join ("\n", R2_Values.ConvertAll (x => x.ToString ()))),
				              6, 7, j, j + 1);
				vbox.Add (sumtable);
			}
			vbox.ShowAll();
		}


		AspectFrame fromCache (string path)
		{
			Image img = ImageCache_.fromCache (path);
			if (img != null) {
				AspectFrame af = new AspectFrame (null, 0.5f, 0.5f, 1, false);
				img.Reparent (af);
				updateAspect (af);
				return af;
			}
			return null;
		}


		AspectFrame getImage (string path)
		{
			AspectFrame image = fromCache(path);
			if (image != null)
				return image;

			return createWidget( new Gdk.Pixbuf (path), path );
		}


		AspectFrame getReferenceImage (string path)
		{
			string isopath = ReferenceStore.isopath (path);

			AspectFrame image = fromCache(isopath);
			if (image != null)
				return image;

			string tooltip="";

			Gdk.Pixbuf pixbuf = null;
			try {
				pixbuf = ReferenceStore.getReferenceImage(path);
			} catch (Exception x) {
				tooltip = x.Message;
				System.Console.WriteLine (x.Message);
				pixbuf = Stetic.IconLoader.LoadIcon ( this, "gtk-dialog-error", IconSize.LargeToolbar);
			}

			image = createWidget( pixbuf, isopath );
			image.TooltipText = tooltip;
			return image;
		}


		AspectFrame createWidget(Gdk.Pixbuf pixbuf, string path)
		{
			AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
			af.Child = ImageCache_.createImage(pixbuf, path);
			updateAspect(af);
			return af;
		}


		void updateAspect (AspectFrame af)
		{
			Image img = af.Child as Image;
			if (img != null && img.Pixbuf != null) {
				af.Set( 0.5f, 0.5f, img.Pixbuf.Width/(float)img.Pixbuf.Height, false );
			}
		}


		void validateImage (AspectFrame testWidget, AspectFrame reference)
		{
			Gdk.Pixbuf pixbuf = null;
			try {
				string path = testWidget.Child.Data ["path"] as string;

				pixbuf = ReferenceStore.validateImage(path);
			} catch (Exception x) {
				statusbartext( x.Message );
				System.Console.WriteLine ( x.Message );
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
				pixbuf.Data["tooltip"] = x.Message;
			}

			if (reference.Child != null)
				reference.Remove(reference.Child);

			reference.Child = ImageCache_.createImage (
				pixbuf, "");

			if (pixbuf.Data.ContainsKey("tooltip"))
				reference.Child.TooltipText = pixbuf.Data["tooltip"] as string;

			reference.ShowAll ();

			Update();
		}


		protected void OnAboutActionActivated (object sender, EventArgs e)
		{
			About about = new About();
			about.Show();
		}

		protected void OnQuitActionActivated (object sender, EventArgs e)
		{
			Destroy();
			Application.Quit ();
		}

		AspectFrame getDiff(AspectFrame reference, AspectFrame newvalue, out string text, out double R2)
		{
			text = "";
			R2 = 0;

			if (newvalue == null)
				return null;
			
			if (reference == null)
				return null;
			
			Image iA = reference.Child as Gtk.Image;
			Image iB = newvalue.Child as Gtk.Image;
			
			if (iA.Pixbuf == null)
				return null;
			if (iB.Pixbuf == null)
				return null;
			
			Gdk.Pixbuf A = iA.Data ["pixbuf"] as Gdk.Pixbuf;
			Gdk.Pixbuf B = iB.Data ["pixbuf"] as Gdk.Pixbuf;

			Gdk.Pixbuf C = ImageCompare.getDiff(A, B, out text, out R2);
			return createWidget( C, "diff");
		}

		void statusbartext (string text)
		{
			var contextId = this.statusbar.GetContextId("clicked");
			this.statusbar.Push(contextId, text );
		}

		protected void OnEntryR2Changed (object sender, EventArgs e)
		{
			double R2 = R2_threshold;
			double.TryParse (this.entryR2.Text, out R2);
			R2 = Math.Max (0.01, Math.Min (1, R2));
			if (R2 != R2_threshold) {
				R2_threshold = R2;
				Update ();
			}
			this.entryR2.Text = R2.ToString("G");
		}

		void updateSettings() {
			MySettings settings = new MySettings();
			settings.R2_Threshold = this.R2_threshold;
			settings.Path = this.entryWatchedFolder.Text;
			settings.save();
		}
	}
}
