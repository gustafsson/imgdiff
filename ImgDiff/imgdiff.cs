using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.Timers;

/**
 * A doodle, merely a work in progess. There's close to not a single best practice being followed here.
 */
namespace ImgDiff
{
	public partial class imgdiff : Window
	{
		double R2_threshold;
		DiffComputer DiffComputer_;
		StatusIcon StatusIcon_;
		System.Windows.Forms.NotifyIcon NotifyIcon_;
		Menu trayPopupMenu;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			this.Build ();

			GLib.ExceptionManager.UnhandledException += HandleUnhandledException;

			MySettings settings = MySettings.load ();
			DiffComputer_ = new DiffComputer ();
			DiffComputer_.DiffListChanged += (DiffComputer sender) => {
				Gtk.Application.Invoke (delegate {
					Update ();
					if (NotifyIcon_ != null && NotifyIcon_.Visible)
						NotifyIcon_.ShowBalloonTip (3);
				});
			};

			if (imgdiff.IsLinux) {
				trayPopupMenu = new Menu ();
				StatusIcon_ = StatusIcon.NewFromStock (Stock.DialogWarning);//new StatusIcon(this.Icon);
				StatusIcon_.Visible = false;
				// Show a pop up menu when the icon has been right clicked.
				StatusIcon_.PopupMenu += OnTrayIconPopup;			
				StatusIcon_.Activate += delegate {
					this.Show ();
				};
			} else {
				NotifyIcon_ = new System.Windows.Forms.NotifyIcon ();
				NotifyIcon_.Visible = false;
				NotifyIcon_.Icon = System.Drawing.SystemIcons.Warning;
				NotifyIcon_.BalloonTipClicked += delegate {
					if (!this.HasFocus) {
						this.Visible = false;
						this.Visible = true;
					}
				};
				NotifyIcon_.Click += delegate {
					if (!this.HasFocus) {
						this.Visible = false;
						this.Visible = true;
					}
				};
			}

			R2_threshold = settings.R2_Threshold;
			this.entryR2.Text = R2_threshold.ToString("G");

			this.entryWatchedFolder.Changed += delegate { HandleNewWatchedFolder(); };
			this.filechooserbutton2.CurrentFolderChanged += delegate {
				string texteditfolder = null;
				try {
					texteditfolder = DiffComputer.GetFileSystemCasing(this.entryWatchedFolder.Text);
				} catch (Exception) {}
				try {
					string filechooserfolder = DiffComputer.GetFileSystemCasing(this.filechooserbutton2.CurrentFolder);
					if ( filechooserfolder != texteditfolder)
						this.entryWatchedFolder.Text = filechooserfolder;
				} catch (Exception) {}
			};

			this.entryWatchedFolder.Text = settings.Path;
		}

		// Create the popup menu, on right click.
		void OnTrayIconPopup (object o, EventArgs args)
		{
			foreach(Widget w in trayPopupMenu.AllChildren) trayPopupMenu.Remove(w);

			DiffComputer.DiffResult result = DiffComputer_.Diff;

			int width = 32; // IconSize.Menu
			int height = 32;
			//float aspectTarget = width / (float)height;
			foreach (PixbufDiff diff in result.List) {
				string name = System.IO.Path.GetFileName(diff.Path);
				ImageMenuItem menuItem = new ImageMenuItem (name + " R2=" + diff.R2.ToString("F4"));
				//float aspectSource = diff.A.Width / (float)diff.A.Height;
				//if (aspectSource < aspectTarget)
				//	menuItem.Image = new Image(diff.A.ScaleSimple((int)(height*aspectSource), height, Gdk.InterpType.Nearest));
				//else
				//	menuItem.Image = new Image(diff.A.ScaleSimple(width, (int)(width/aspectSource), Gdk.InterpType.Nearest));*/
				trayPopupMenu.Add(menuItem);

				// Activate the application when anything is clicked.
				menuItem.Activated += delegate {
					this.scrollTo(name); 
				};
			}
			trayPopupMenu.ShowAll();
			trayPopupMenu.Popup();
			trayPopupMenu.Activate();
		}

		void scrollTo (string name)
		{
			if (scrolledwindow1.Child == null)
				return;

			Bin c = (scrolledwindow1.Child as Bin);
			VBox vbox = c.Child as VBox;
			if (vbox == null || vbox.Children.Length < 1)
				return;

			Table table = vbox.Children [0] as Table;
			if (table == null)
				return;

			foreach (Widget child in table.AllChildren) {
				Label label = child as Label;
				if (label == null )
					continue;

				if (label.Text == name)
				{
					scrolledwindow1.Vadjustment.Value = label.Allocation.Top;
					return;
				}
			}
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
			if (DiffComputer_.Path == folder)
				return;

			if (Directory.Exists (folder))
				this.filechooserbutton2.SetCurrentFolder (new DirectoryInfo(folder).FullName);

			DiffComputer_.Path = folder;
		}


		void Update ()
		{
			updateSettings ();

			DiffComputer.DiffResult result = DiffComputer_.Diff;
			string folder = result.Path;

			List<PixbufDiff> diff = new List<PixbufDiff> (result.List);
			List<PixbufDiff> oklist = new List<PixbufDiff> (result.List);
			oklist.RemoveAll (x => x.R2 < this.R2_threshold);
			diff.RemoveAll (x => x.R2 >= this.R2_threshold);

			Table table = new Table (Math.Max (1, 2 * (uint)diff.Count), 7u, false);
			for (int i=0; i<diff.Count; ++i) {
				AspectFrame af = createAspectFrame (diff [i].A);
				AspectFrame af2 = createAspectFrame (diff [i].B);
				Widget middlewidget = createDiffWidget (af, af2, diff [i]);

				string filename = System.IO.Path.GetFileName (diff [i].Path);
				Label nametext = new Label (filename);
				Label nametext2 = new Label (diff [i].B == null ? "" : filename);

				uint j = 2 * (uint)i + 2;
				table.Attach (nametext, 0u, 3u, j - 2, j - 1);
				table.Attach (nametext2, 4u, 7u, j - 2, j - 1);
				table.Attach (af, 0u, 3u, j - 1, j);
				table.Attach (middlewidget, 3u, 4u, j - 1, j);
				table.Attach (af2, 4u, 7u, j - 1, j);
			}

			if (0 == diff.Count) {
				if (oklist.Count > 0) {
					table.Attach (new Label ("All images were compared within the threshold to the stored references files"),
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
					statusbartext ("Folder " + folder + " does not exist");
				} else
					statusbartext ("Found no image files in folder " + folder + "");
			} else {
				if (diff.Count == 1)
					statusbartext (string.Format ("\"{0}\" does not match its reference image", System.IO.Path.GetFileName (diff [0].Path)));
				else
					statusbartext (string.Format ("{0} of {1} images do not match their reference images", diff.Count, diff.Count + oklist.Count));
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
				Table sumtable = new Table (1, 7, false);
				sumtable.Attach (new Label ("Files within threshold\n" 
					+ String.Join ("\n", oklist.ConvertAll (x => System.IO.Path.GetFileName (x.Path)))),
				              0, 6, 0, 1);
				sumtable.Attach (new Label ("R2\n" 
					+ String.Join ("\n", oklist.ConvertAll (x => x.R2.ToString ()))),
				              6, 7, 0, 1);
				vbox.Add (sumtable);
			}
			vbox.ShowAll ();

			string tooltip = "";
			if (diff.Count == 1)
				tooltip = string.Format ("\"{0}\" does not match its reference image. R2 = {1}", System.IO.Path.GetFileName (diff [0].Path), diff [0].R2.ToString ("F4"));
			else if (diff.Count > 0)
				tooltip = string.Format ("{0} of {1} images do not match their reference images:\n", diff.Count, diff.Count + oklist.Count,
				                              String.Join ("\n", diff.ConvertAll (x => System.IO.Path.GetFileName (x.Path) + " R2 = " + x.R2.ToString ())));

			if (StatusIcon_ != null) {
				StatusIcon_.Tooltip = tooltip;
				StatusIcon_.Visible = diff.Count > 0;
			}
			if (NotifyIcon_ != null) {
				NotifyIcon_.BalloonTipTitle = folder;
				NotifyIcon_.BalloonTipText = tooltip;
				NotifyIcon_.Visible = diff.Count > 0;
			}
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
			infotext.LineWrap = true;
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
				ReferenceStore.validateImage (path);
				DiffComputer_.LaunchRecompute();
			} catch (Exception x) {
				statusbartext (x.Message);
				System.Console.WriteLine (x.Message);
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
				pixbuf.Data ["tooltip"] = x.Message;

				if (reference.Child != null)
					reference.Remove (reference.Child);

				reference.Child = PixbufCache.createImage (pixbuf);
				updateAspect (reference);

				if (pixbuf.Data.ContainsKey ("tooltip"))
					reference.Child.TooltipText = pixbuf.Data ["tooltip"] as string;

				reference.ShowAll ();
			}
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
			UpdateR2();
		}

		void updateSettings() {
			MySettings settings = new MySettings();
			settings.R2_Threshold = this.R2_threshold;
			settings.Path = this.entryWatchedFolder.Text;
			settings.save();
		}

		protected void OnEntryR2FocusOutEvent (object o, FocusOutEventArgs args)
		{
			ValidateR2();
		}

		protected void OnEntryR2KeyReleaseEvent (object o, KeyReleaseEventArgs args)
		{
			if (args.Event.Key == Gdk.Key.Return)
				ValidateR2();
		}

		void UpdateR2 () {
			double R2 = R2_threshold;
			if (!double.TryParse (this.entryR2.Text, out R2))
				return;
			R2 = Math.Min (1, R2);
			if (R2 != R2_threshold) {
				R2_threshold = R2;
				Update ();
			}
		}

		void ValidateR2 ()
		{
			UpdateR2();
			this.entryR2.Text = R2_threshold.ToString("G");
		}


		public static bool IsLinux
		{
			get
			{
				int p = (int) Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}
	}
}
