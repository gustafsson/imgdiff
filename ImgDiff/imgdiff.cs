using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Diagnostics;
using System.Timers;

/**
 * A doodle, merely a work in progess. There's close to not a single best practice being followed here.
 */
namespace ImgDiff
{
	public partial class imgdiff : Window
	{
		FileSystemWatcher watcher_;
		Dictionary<string,Image> images_;
		double R2_threshold;

		CheckModifiedFiles CheckModifiedFiles_;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			this.Build ();

			GLib.ExceptionManager.UnhandledException += HandleUnhandledException;

			R2_threshold = 0.9999;
			this.entryR2.Text = R2_threshold.ToString("G");

			CheckModifiedFiles_ = new CheckModifiedFiles();
			CheckModifiedFiles_.FoundModifiedFile += (object sender, string[] files) => {
				foreach(string file in files) images_.Remove( file );
				WatcherUpdate( null );
			};

			this.images_ = new Dictionary<string,Image>();
			this.watcher_ = new FileSystemWatcher ();
			watcher_.Changed += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Created += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Deleted += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Renamed += (object sender, RenamedEventArgs e) => WatcherUpdate (e);
			watcher_.Error += (object sender, ErrorEventArgs e) => WatcherUpdate (e);

			this.entryWatchedFolder.Changed += (sender, e) => HandleNewWatchedFolder();
			this.filechooserbutton2.CurrentFolderChanged += (object sender, EventArgs e) => {
				if ( string.IsNullOrWhiteSpace(this.entryWatchedFolder.Text)
				    || new DirectoryInfo(this.entryWatchedFolder.Text).FullName != this.filechooserbutton2.CurrentFolder)
				{
					this.entryWatchedFolder.Text = this.filechooserbutton2.CurrentFolder;
				}
			};

			this.filechooserbutton2.SetCurrentFolder( this.filechooserbutton2.CurrentFolder );
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

			if (watcher_ != null && watcher_.EnableRaisingEvents == true && watcher_.Path == folder)
				return;

			if (Directory.Exists (folder)) {
				this.filechooserbutton2.SetCurrentFolder (folder);
				watcher_.Path = folder;
				CheckModifiedFiles_.Path = folder;
				watcher_.EnableRaisingEvents = true;
			}
			else
				watcher_.EnableRaisingEvents = false;

			Update();
		}

		void WatcherUpdate (EventArgs e)
		{
			Gtk.Application.Invoke( delegate {
				Update();
			});
		}

		void Update ()
		{
			Stopwatch watch = new Stopwatch ();
			watch.Start ();

			string[] files;
			string folder = this.entryWatchedFolder.Text;
			if (Directory.Exists (folder))
				files = Directory.GetFiles (folder);
			else
				files = new string[0];

			uint j = 0;

			Dictionary<string,Image> newimages = new Dictionary<string,Image> ();

			Table table = new Table (Math.Max (1, 2 * (uint)files.Length), 7u, false);
			Gdk.Color col = new Gdk.Color ();
			Gdk.Color.Parse ("red", ref col);
			List<string> imagefiles = new List<string> ();
			List<double> R2_Values = new List<double> ();
			for (int i=0; i<files.Length; ++i) {
				try {
					if (equalfiles (files [i]))
					{
						imagefiles.Add (files [i]);
						R2_Values.Add (1);
						continue;
					}

					AspectFrame af = getImage (files [i]);
					AspectFrame af2 = getReferenceImage (files [i]);

					updateCache (newimages, af);
					updateCache (newimages, af2);

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

			this.images_ = newimages;

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

		AspectFrame getImage (string path)
		{
			AspectFrame image = fromCache(path);
			if (image != null)
				return image;

			return createWidget( new Gdk.Pixbuf (path), path );
		}


		AspectFrame getReferenceImage (string path)
		{
			string isopath = this.isopath (path);

			AspectFrame image = fromCache(isopath);
			if (image != null)
				return image;

			Gdk.Pixbuf pixbuf = null;
			string tooltip="";

			try {
				using (IsolatedStorageFile isoStore = this.isoStore()) {
					if (isoStore.FileExists (isopath))
					{
						using (IsolatedStorageFileStream isf = isoStore.OpenFile (isopath, FileMode.Open)) {
							pixbuf = new Gdk.Pixbuf (isf);
						}
					}
				}
			} catch (Exception x) {
				tooltip = x.Message;
				System.Console.WriteLine (x.Message);
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
			}

			image = createWidget( pixbuf, isopath );
			image.TooltipText = tooltip;
			return image;
		}


		AspectFrame createWidget(Gdk.Pixbuf pixbuf, string path)
		{
			AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
			createImage(af, pixbuf, path);
			return af;
		}


		void createImage (AspectFrame af, Gdk.Pixbuf pixbuf, string path)
		{
			if (pixbuf!=null)
				System.Console.WriteLine (string.Format("Creating {0}image of {1}",
			                                        pixbuf==null?"empty ":"", 
			                                        System.IO.Path.GetFileName(path)));

			Image img = new Image ();
			img.Name = null;
			img.Pixbuf = pixbuf;
			img.Data["pixbuf"] = pixbuf;
			img.Data["path"] = path;
			img.SetSizeRequest( 0, 0 );
			if (af.Child != null)
				af.Remove(af.Child);
			af.Child = img;

			if (pixbuf != null)
			{
				af.Set( 0.5f, 0.5f, pixbuf.Width/(float)pixbuf.Height, false );

				img.SizeAllocated += (o, args) =>
				{
					Image im = (o as Image);
					Gdk.Pixbuf pb = (im.Data["pixbuf"] as Gdk.Pixbuf);

					if (im.Pixbuf.Width != args.Allocation.Width || im.Pixbuf.Height != args.Allocation.Height)
					{
//						string p = im.Data["path"] as string;
//						System.Console.WriteLine (string.Format("Resizing {0} to {1}x{2} from origin {3}x{4}",
//						                                        System.IO.Path.GetFileName(p),
//						                                        args.Allocation.Width, args.Allocation.Height,
//						                                        pb.Width, pb.Height));
						im.Pixbuf = pb.ScaleSimple(args.Allocation.Width, args.Allocation.Height, Gdk.InterpType.Nearest);
					}
				};
			}
		}


		void validateImage (AspectFrame testWidget, AspectFrame reference)
		{
			Gdk.Pixbuf pixbuf = null;

			Widget w = testWidget.Child;
			string path = w.Data ["path"] as string;
			string isopath = this.isopath (path);

			try {
				using (IsolatedStorageFile isoStore = this.isoStore()) {
					// copy file int o isolated storage
					using (IsolatedStorageFileStream output = isoStore.OpenFile(isopath, FileMode.Create)) {
						using (FileStream input = new FileStream(path, FileMode.Open)) {
							CopyStream (input, output);
						}
					}

					using (IsolatedStorageFileStream input = isoStore.OpenFile (isopath, FileMode.Open)) {
						pixbuf = new Gdk.Pixbuf (input);
					}
				}
			} catch (Exception x) {
				System.Console.WriteLine (x.Message);
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
				//reference.TooltipText = x.Message;
			}


			createImage (
				reference,
				pixbuf,
			    isopath);


			reference.ShowAll ();

			Update();
		}

		string isopath(string path)
		{
			/*
			string isopath = path;
			isopath = isopath.Replace(System.IO.Path.AltDirectorySeparatorChar, '_');
			isopath = isopath.Replace(System.IO.Path.DirectorySeparatorChar, '_');
			isopath = isopath.Replace(System.IO.Path.PathSeparator, '_');
			isopath = isopath.Replace(System.IO.Path.VolumeSeparatorChar, '_');
			isopath = isopath + path.GetHashCode().ToString();
			*/
			return System.IO.Path.GetFileNameWithoutExtension ( path ) 
				+ path.GetHashCode().ToString() 
					+ System.IO.Path.GetExtension (path);
		}

		IsolatedStorageFile isoStore ()
		{
			return IsolatedStorageFile.GetUserStoreForAssembly();
		}

		public static void CopyStream(Stream input, Stream output)
		{
			byte[] buffer = new byte[32768];
			int read;
			while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write (buffer, 0, read);
			}
		}

		void updateCache( Dictionary<string,Image> newimages, AspectFrame af )
		{
			Image im = af.Child as Image;
			newimages [im.Data["path"] as string] = im;
		}

		AspectFrame fromCache(string path)
		{
			if (images_.ContainsKey (path)) {
				AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
				Image image = images_ [path];
				Gdk.Pixbuf pixbuf = image.Data["pixbuf"] as Gdk.Pixbuf;
				if (pixbuf != null)
					af.Set( 0.5f, 0.5f, pixbuf.Width/(float)pixbuf.Height, false );

				image.Reparent(af);

				return af;
			}
			return null;
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

		bool equalfiles (string path)
		{
			using (IsolatedStorageFile isoStore = this.isoStore()) {
				string isopath = this.isopath (path);
				if (!isoStore.FileExists (isopath))
					return false;

				using (IsolatedStorageFileStream input1 = isoStore.OpenFile(isopath, FileMode.Open)) {
					using (FileStream input2 = new FileStream(path, FileMode.Open)) {
						byte[] buffer1 = new byte[32768];
						byte[] buffer2 = new byte[32768];
						int read1, read2;
						while (true) {
							read1 = input1.Read (buffer1, 0, buffer1.Length);
							read2 = input2.Read (buffer2, 0, buffer2.Length);
							if (read1 != read2)
								return false;
							if (read1 <= 0)
								break;
							//if (!Array.Equals(buffer1, buffer2))
							//    return false;
							if (!ImageCompare.UnsafeCompare(buffer1, buffer2))
								return false;
						}
					}
				}
			}

			return true;
		}



		protected void OnEntryR2Changed (object sender, EventArgs e)
		{
			double R2 = R2_threshold;
			double.TryParse (this.entryR2.Text, out R2);
			R2 = Math.Max (0.01, Math.Min (1, R2));
			if (R2 != R2_threshold) {
				R2_threshold = R2;
				WatcherUpdate (null);
			}
			this.entryR2.Text = R2.ToString("G");
		}
	}
}
