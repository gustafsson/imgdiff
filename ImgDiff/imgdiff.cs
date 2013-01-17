using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Diagnostics;

/**
 * A doodle, merely a work in progess. There's close to not a single best practice being followed here.
 */
namespace ImgDiff
{
	public partial class imgdiff : Window
	{
		FileSystemWatcher watcher_;
		Dictionary<string,Image> images_;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			this.Build ();

			this.images_ = new Dictionary<string,Image>();
			this.watcher_ = new FileSystemWatcher ();

			this.entryWatchedFolder.TextInserted += HandleTextInserted;
			this.filechooserbutton2.CurrentFolderChanged += (object sender, EventArgs e) => this.entryWatchedFolder.Text = this.filechooserbutton2.CurrentFolder;
			//this.filechooserbutton2.SetCurrentFolder(this.filechooserbutton2.CurrentFolder); // Call event handler

			this.entryWatchedFolder.Text = "/Users/johan/Desktop/tmp";
		}

		protected void OnDeleteEvent (object sender, DeleteEventArgs a)
		{
			Application.Quit ();
			a.RetVal = true;
		}

		void HandleTextInserted (object o, TextInsertedArgs args)
		{
			string folder = this.entryWatchedFolder.Text;

			if (!Directory.Exists(folder))
				return;

			if (watcher_ != null && watcher_.Path == folder)
				return;

			this.filechooserbutton2.SetCurrentFolder( folder );
			watcher_.Path = folder;
			watcher_.EnableRaisingEvents = true;
			watcher_.Changed += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate ();
			watcher_.Created += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate ();
			watcher_.Deleted += (object sender, System.IO.FileSystemEventArgs e) => WatcherUpdate ();
			watcher_.Renamed += (object sender, RenamedEventArgs e) => WatcherUpdate ();
			watcher_.Error += (object sender, ErrorEventArgs e) => WatcherUpdate ();

			Update();
		}

		void WatcherUpdate ()
		{
			Gtk.Application.Invoke( delegate {
				Update();
			});
		}

		void Update ()
		{
			Stopwatch watch = new Stopwatch ();
			watch.Start ();

			string[] files = Directory.GetFiles (this.entryWatchedFolder.Text);
			uint j = 0;

			Dictionary<string,Image> newimages = new Dictionary<string,Image> ();

			Table table = new Table (Math.Max (1, (uint)files.Length), 3u, false);
			for (int i=0; i<files.Length; ++i) {
				try {
					AspectFrame af = getImage (files [i]);
					AspectFrame af2 = getReferenceImage (files [i]);

					updateCache (newimages, af);
					updateCache (newimages, af2);

					Image arrow = new Image ();
					arrow.Pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-go-forward", IconSize.LargeToolbar);
					Button button = new Button ();
					button.Add (arrow);
					button.Clicked += (object sender, EventArgs e) => validateImage (af, af2);

					j++;

					table.Attach (af, 0u, 1u, j - 1, j);
					table.Attach (button, 1u, 2u, j - 1, j);
					table.Attach (af2, 2u, 3u, j - 1, j);
				} catch (Exception x) {
					System.Console.WriteLine (x.Message);
				}
			}

			if (j < table.NRows) {
				//this.table1.Attach (null, 0u, 3u, j, this.table1.NRows);
				table.Resize (Math.Max (1, j), table.NColumns);
			}

			if (scrolledwindow1.Child != null) {
				Bin c = (scrolledwindow1.Child as Bin);
				if (null != c.Child)
					c.Remove (c.Child);
			}

			VBox vbox = new VBox ();
			scrolledwindow1.AddWithViewport (vbox);

			this.images_ = newimages;

			watch.Stop ();
			System.Console.WriteLine (string.Format ("Updated {1} files in {0} s", watch.ElapsedMilliseconds * 1e-3, j));

			vbox.Add (table);
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
				reference.TooltipText = x.Message;
			}


			createImage (
				reference,
				pixbuf,
			    isopath);


			reference.ShowAll ();
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
	}
}
