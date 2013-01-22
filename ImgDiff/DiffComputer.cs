using System;
using System.Diagnostics;
using System.IO;
using Gdk;
using System.Collections.Generic;
using System.Timers;
using System.Threading;

namespace ImgDiff
{
	public class DiffComputer
	{
		public class DiffResult 
		{
			public List<PixbufDiff> List { get; private set; }
			public string Path { get; private set; }

			public DiffResult(List<PixbufDiff> List, string Path) {
				this.List = List;
				this.Path = Path;
			}
		}

		object DiffResultLock_ = new object();

		public DiffResult Diff {
			get {
				lock (DiffResultLock_) {
					return DiffResult_;
				}
			}
			private set {
				lock (DiffResultLock_) {
					DiffResult_ = value;
				}
			}
		}

		public string Path {
			get {
				return FixedFileWatcher_.Path;
			}
			set {
				FixedFileWatcher_.Path = value;

				LaunchRecompute();
			}
		}

		public delegate void ChangedEventHandler(DiffComputer sender);
		public event ChangedEventHandler DiffListChanged;

		FixedFileWatcher FixedFileWatcher_;
		PixbufCache PixbufCache_;
		PixbufCache ReferenceCache_;
		DiffResult DiffResult_;
		object recopmuteThreadLock_ = new object();
		Thread recomputeThread_;

		public DiffComputer ()
		{
			PixbufCache_ = new PixbufCache();
			ReferenceCache_ = new PixbufCache();
			recomputeThread_ = null;
			FixedFileWatcher_ = new FixedFileWatcher();
			FixedFileWatcher_.Changed += (object sender, string file) => {
				PixbufCache_.prune(file);
				LaunchRecompute();
			};
		}

		public void LaunchRecompute ()
		{
			// Launch DiffComputer in a new thread.
			lock (recopmuteThreadLock_) {
				if (recomputeThread_ != null) {
					recomputeThread_.Abort ();
					recomputeThread_.Join ();
				}
				recomputeThread_ = new Thread(this.recomputeDiff);
				recomputeThread_.Start();
			}
		}

		void recomputeDiff ()
		{
			Stopwatch watch = new Stopwatch ();
			watch.Start ();
	
			string folder = Path;
			IEnumerable<FileInfo> files = null;
			if (Directory.Exists (folder)) {
				DirectoryInfo di = new DirectoryInfo (folder);
				if (di.Exists)
				{
					try {
						di = new DirectoryInfo (GetFileSystemCasing(folder));
						files = di.EnumerateFiles ();
					} catch (System.Exception) {}
				}
			}
			if (files == null)
				files = new FileInfo[0];

			PixbufCache_.flagToPrune ();
			ReferenceCache_.flagToPrune ();
			List<PixbufDiff> diffs = new List<PixbufDiff> ();
			foreach (FileInfo fi in files) { // for (int i=0; i<files.Length; ++i) {
				string path = fi.FullName;
				PixbufDiff diff;
				if (ReferenceStore.equalfiles (path)) {
					diff = new PixbufDiff (null, null, path);
				} else {
					Pixbuf A = PixbufCache_.fromCache (path);
					if (A == null) {
						try {
							using (FileStream fs = fi.OpenRead()) {
								A = new Pixbuf (fs);
								PixbufCache_.updateCache (path, A);
							}
						} catch (GLib.GException x) {
							if (x.Message == "Unrecognized image file format") {
								// Not an image file
							} else if (x.Message == "Image has zero width") {
								// ignore
							} else {
								System.Console.WriteLine (path + " " + x.Message);
							}
							continue;
						} catch (Exception x) {
							System.Console.WriteLine (path + " " + x.Message);
							continue;
						}
					}
			
					Pixbuf B = ReferenceCache_.fromCache (path);
					if (B == null) {
						try {
							B = ReferenceStore.getReferenceImage (path);
							if (B != null)
								ReferenceCache_.updateCache (path, B);
						} catch (Exception x) {
							B = imgdiff.ErrorPixbuf;
							B.Data ["tooltip"] = x.Message;
						}
					}
			
					// Compute a diff
					diff = new PixbufDiff (A, B, path);
				}
		
				diffs.Add (diff);
			}
	
			PixbufCache_.prune ();
			ReferenceCache_.prune ();
	
			watch.Stop ();
	
			System.Console.WriteLine (string.Format ("Computed diff of '{2}' with {1} files in {0} s", watch.ElapsedMilliseconds * 1e-3, diffs.Count, folder));

			this.Diff = new DiffResult (diffs, folder);

			if (DiffListChanged != null)
				DiffListChanged (this);
		}

		static public string GetFileSystemCasing(string path)
		{
			if (System.IO.Path.IsPathRooted(path))
			{
				path = path.TrimEnd(System.IO.Path.DirectorySeparatorChar); // if you type c:\foo\ instead of c:\foo
				try
				{
					string name = System.IO.Path.GetFileName(path);
					if (name == "") return path.ToUpper() + System.IO.Path.DirectorySeparatorChar; // root reached
					
					string parent = System.IO.Path.GetDirectoryName(path); // retrieving parent of element to be corrected
					
					parent = GetFileSystemCasing(parent); //to get correct casing on the entire string, and not only on the last element
					
					DirectoryInfo diParent = new DirectoryInfo(parent);
					FileSystemInfo[] fsiChildren = diParent.GetFileSystemInfos(name);
					FileSystemInfo fsiChild = fsiChildren[0];
					return fsiChild.FullName; // coming from GetFileSystemImfos() this has the correct case
				}
				catch (Exception ex) { Trace.TraceError(ex.Message); throw new ArgumentException("Invalid path"); }
			}
			else throw new ArgumentException("Absolute path needed, not relative");
		}

		public static void test ()
		{
		}
	}
}

