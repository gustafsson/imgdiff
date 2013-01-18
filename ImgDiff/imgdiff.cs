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
		PixbufCache PixbufCache_;
		PixbufCache ReferenceCache_;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			this.Build ();

			MySettings settings = MySettings.load();

			GLib.ExceptionManager.UnhandledException += HandleUnhandledException;

			R2_threshold = settings.R2_Threshold;
			this.entryR2.Text = R2_threshold.ToString("G");

			PixbufCache_ = new PixbufCache();
			ReferenceCache_ = new PixbufCache();

			FixedFileWatcher_ = new FixedFileWatcher();
			FixedFileWatcher_.Changed += (object sender, string[] files) => {
				Gtk.Application.Invoke( delegate {
					PixbufCache_.prune(files);
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


		List<PixbufDiff> getDiff(string folder)
		{
			Stopwatch watch = new Stopwatch ();
			watch.Start ();

			string[] files;
			if (Directory.Exists (folder))
				files = Directory.GetFiles (folder);
			else
				files = new string[0];

			PixbufCache_.flagToPrune ();
			ReferenceCache_.flagToPrune ();
			List<PixbufDiff> diffs = new List<PixbufDiff> ();
			for (int i=0; i<files.Length; ++i) {
				string path = files[i];
				PixbufDiff diff;
				if (ReferenceStore.equalfiles (files [i]))
				{
					diff = new PixbufDiff(null, null, files[i]);
				}
				else
				{
					Gdk.Pixbuf A = PixbufCache_.fromCache (path);
					if (A == null) {
						try
						{
							A = new Gdk.Pixbuf (path);
							PixbufCache_.updateCache (path, A);
						} catch (GLib.GException x) {
							if (x.Source == "gdk-sharp") {
								// Not an image file
							} else {
								System.Console.WriteLine (x.Message);
							}
							continue;
						} catch (Exception x) {
							if (x.Source == "gdk-sharp") {
								// Not an image file
							} else {
								System.Console.WriteLine (x.Message);
							}
							continue;
						}
					}

					Gdk.Pixbuf B = ReferenceCache_.fromCache (path);
					if (B == null) {
						try
						{
							B = ReferenceStore.getReferenceImage (path);
							ReferenceCache_.updateCache (path, B);
						} catch (Exception x) {
							B = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
							B.Data["tooltip"] = x.Message;
						}
					}

					// Compute a diff
					diff = new PixbufDiff(A, B, path);
				}

				diffs.Add(diff);
			}

			PixbufCache_.prune();
			ReferenceCache_.prune();

			watch.Stop();

			System.Console.WriteLine (string.Format ("Computed diff of {1} files in {0} s", watch.ElapsedMilliseconds * 1e-3, diffs.Count));

			return diffs;
		}

		void Update ()
		{
			updateSettings ();

			string folder = this.entryWatchedFolder.Text;

			List<PixbufDiff> diff = getDiff (folder);
			List<PixbufDiff> oklist = new List<PixbufDiff>(diff);
			oklist.RemoveAll (x => x.R2 < this.R2_threshold);
			diff.RemoveAll (x => x.R2 >= this.R2_threshold);

			Table table = new Table (Math.Max (1, 2 * (uint)diff.Count), 7u, false);
			for (int i=0; i<diff.Count; ++i) {
				AspectFrame af = createAspectFrame (diff [i].A);
				AspectFrame af2 = createAspectFrame (diff [i].B);
				Widget middlewidget = createDiffWidget (af, af2, diff [i]);

				Label nametext = new Label (System.IO.Path.GetFileName (diff [i].Path));
				Label nametext2 = new Label (System.IO.Path.GetFileName (diff [i].Path));

				uint j = 2 * (uint)i;
				table.Attach (nametext, 0u, 3u, j - 2, j - 1);
				table.Attach (nametext2, 4u, 7u, j - 2, j - 1);
				table.Attach (af, 0u, 3u, j - 1, j);
				table.Attach (middlewidget, 3u, 4u, j - 1, j);
				table.Attach (af2, 4u, 7u, j - 1, j);
			}

			if (0 == diff.Count) {
				if (oklist.Count > 0) {
					table.Attach (new Label ("All images were compared within the threshold to the stored references files!"),
					              0, 7, 0, 1);

					int noneidentical_thresholded = 0;
					oklist.ForEach (x => noneidentical_thresholded += x.R2 < 1 ? 1 : 0);
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
			} else {
				int j = diff.Count;
				statusbartext (string.Format ("{0} image{1} not match reference image{1}", j, j == 1 ? " does" : "s do"));
			}

			if (scrolledwindow1.Child != null) {
				Bin c = (scrolledwindow1.Child as Bin);
				if (null != c.Child)
					c.Remove (c.Child);
			}

			VBox vbox = new VBox ();
			scrolledwindow1.AddWithViewport (vbox);

			vbox.Add (table);
			Box.BoxChild bc = ((Box.BoxChild)(vbox [table]));
			bc.Expand = false;

			if (oklist.Count > 0) {
				Table sumtable = new Table(1, 7, false);
				sumtable.Attach (new Label ("Files within threshold\n" 
				                            + String.Join ("\n", oklist.ConvertAll (x => System.IO.Path.GetFileName (x.Path)))),
				              0, 6, 0, 1);
				sumtable.Attach (new Label ("R2\n" 
				                            + String.Join ("\n", oklist.ConvertAll (x => x.R2.ToString ()))),
				              6, 7, 0, 1);
				vbox.Add (sumtable);
			}
			vbox.ShowAll();
		}

		AspectFrame createAspectFrame(Gdk.Pixbuf pixbuf)
		{
			AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
			af.Child = PixbufCache.createImage(pixbuf);
			updateAspect(af);
			return af;
		}

		Widget createDiffWidget(AspectFrame af, AspectFrame af2, PixbufDiff diff)
		{
			VBox sumbox = new VBox ();
			
			sumbox.Add (createAspectFrame( diff.Diff ));

			Label infotext = new Label (diff.diffstring);
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
			button.Clicked += (object sender, EventArgs e) => validateImage (diff.Path, af2);
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
			return sumbox;
		}

		void updateAspect (AspectFrame af)
		{
			Image img = af.Child as Image;
			if (img != null && img.Pixbuf != null) {
				af.Set( 0.5f, 0.5f, img.Pixbuf.Width/(float)img.Pixbuf.Height, false );
			}
		}


		void validateImage (string path, AspectFrame reference)
		{
			Gdk.Pixbuf pixbuf = null;
			try {
				ReferenceStore.validateImage(path);
				pixbuf = ReferenceStore.getReferenceImage(path);
			} catch (Exception x) {
				statusbartext( x.Message );
				System.Console.WriteLine ( x.Message );
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
				pixbuf.Data["tooltip"] = x.Message;
			}

			if (reference.Child != null)
				reference.Remove(reference.Child);

			reference.Child = PixbufCache.createImage ( pixbuf );
			updateAspect(reference);

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
