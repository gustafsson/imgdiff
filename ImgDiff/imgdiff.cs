using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Diagnostics;

namespace ImgDiff
{
	public partial class imgdiff : Window
	{
		FileSystemWatcher watcher_;
		Dictionary<string,Widget> images_;

		public imgdiff () : 
				base(WindowType.Toplevel)
		{
			// Clear isolated storage
			foreach(string file in isoStore().GetFileNames())
				isoStore().DeleteFile(file);

			this.Build ();

			this.images_ = new Dictionary<string,Widget>();

			this.entryWatchedFolder.TextInserted += HandleTextInserted;
			this.filechooserbutton2.CurrentFolderChanged += (object sender, EventArgs e) => this.entryWatchedFolder.Text = this.filechooserbutton2.CurrentFolder;
			//this.filechooserbutton2.SetCurrentFolder(this.filechooserbutton2.CurrentFolder); // Call event handler

			this.entryWatchedFolder.Text = "/Users/johan/Desktop/tmp";
		}

		void HandleTextInserted (object o, TextInsertedArgs args)
		{
			string folder = this.entryWatchedFolder.Text;

			if (!Directory.Exists(folder))
				return;

			if (watcher_ != null && watcher_.Path == folder)
				return;

			this.filechooserbutton2.SetCurrentFolder( folder );
		    watcher_ = new FileSystemWatcher (folder);
			watcher_.EnableRaisingEvents = true;
			watcher_.Changed += (object sender, System.IO.FileSystemEventArgs e) => Update ();
			watcher_.Created += (object sender, System.IO.FileSystemEventArgs e) => Update ();
			watcher_.Deleted += (object sender, System.IO.FileSystemEventArgs e) => Update ();
			watcher_.Renamed += (object sender, RenamedEventArgs e) => Update ();
			watcher_.Error += (object sender, ErrorEventArgs e) => Update ();

			Update();
		}

		void Update ()
		{
			Stopwatch watch = new Stopwatch();
			watch.Start();

			string[] files = Directory.GetFiles (this.entryWatchedFolder.Text);
			uint j = 0;

			Dictionary<string,Widget> newimages = new Dictionary<string,Widget> ();
			Gtk.Table table = new Table(this.table1.NRows, 3, false);
			for (int i=0; i<files.Length; ++i) {
				try {
					Widget af = getImage (files [i]);
					newimages [files [i]] = af;

					Widget af2 = getReferenceImage (files [i]);
					newimages [files [i]] = af2;

					Image arrow = new Image ();
					arrow.Pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-go-forward", IconSize.LargeToolbar);
					Button button = new Button();
					button.Add (arrow);
					button.Clicked += (object sender, EventArgs e) => validateImage (af, af2);

					j++;
					if (null != Array.Find( table1.Children, c ) )
						table1.Remove (c);

					table.Attach (af, 0u, 1u, j - 1, j);
					table.Attach (button, 1u, 2u, j - 1, j);
					table.Attach (af2, 2u, 3u, j - 1, j);
				} catch (Exception x) {
					System.Console.WriteLine (x.Message);
				}
			}

			if (j < this.table1.NRows) {
				//this.table1.Attach (null, 0u, 3u, j, this.table1.NRows);
				this.table1.Resize( j, this.table1.NColumns );
			}

			foreach (Widget c in scrolledwindow1.Children) scrolledwindow1.Remove(c);
			scrolledwindow1.Child = table;
			this.images_ = newimages;
			scrolledwindow1.ShowAll();

			watch.Stop();
			System.Console.WriteLine(string.Format("Updated {1} files in {0} s", watch.ElapsedMilliseconds*1e-3, j));
		}

		Widget getImage (string path)
		{
			if (images_.ContainsKey (path)) {
				Widget image = images_ [path];
				(image.Parent as Container).Remove(image);
				return image;
			}

			return createWidget( new Gdk.Pixbuf (path), path );
		}


		Widget getReferenceImage (string path)
		{
			string isopath = this.isopath (path);

			if (images_.ContainsKey (isopath)) {
				Widget image = images_ [path];
				(image.Parent as Container).Remove(image);
				return image;
			}

			Gdk.Pixbuf pixbuf = null;

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
				System.Console.WriteLine (x.Message);
				pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
			}

			return createWidget( pixbuf, path );
		}


		Widget createWidget(Gdk.Pixbuf pixbuf, string path)
		{
			AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
			createImage(af, pixbuf, path);
			return af;
		}


		void createImage (AspectFrame af, Gdk.Pixbuf pixbuf, string path)
		{
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
						string p = im.Data["path"] as string;
						System.Console.WriteLine (string.Format("Resizing {0} to {1}x{2} from origin {3}x{4}",
						                                        System.IO.Path.GetFileName(p),
						                                        args.Allocation.Width, args.Allocation.Height,
						                                        pb.Width, pb.Height));
						im.Pixbuf = pb.ScaleSimple(args.Allocation.Width, args.Allocation.Height, Gdk.InterpType.Nearest);
					}
				};
			}
		}


		void validateImage (Widget testWidget, Widget reference)
		{
			Gdk.Pixbuf pixbuf = null;

			Widget w = (testWidget as AspectFrame).Child;
			string path = w.Data ["path"] as string;

			try {

				string isopath = this.isopath (path);

				using (IsolatedStorageFile isoStore = this.isoStore()) {
					// copy file int o isolated storage
					using (IsolatedStorageFileStream output = isoStore.OpenFile(isopath, FileMode.Create)) {
						using (FileStream input = new FileStream(path, FileMode.Open)) {
							CopyStream(input, output);
						}
					}

					pixbuf = new Gdk.Pixbuf (isoStore.OpenFile (isopath, FileMode.Open));
				}
			} catch (Exception x) {
					System.Console.WriteLine(x.Message);
					pixbuf = Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", IconSize.LargeToolbar);
			}
			createImage (
					(reference as AspectFrame),
					pixbuf,
					path);

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
	}
}
