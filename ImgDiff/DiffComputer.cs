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
			FixedFileWatcher_.Changed += (object sender, string[] files) => {
				PixbufCache_.prune(files);
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
	
			string[] files;
			string folder = Path;
			if (Directory.Exists (folder))
				files = Directory.GetFiles (folder);
			else
				files = new string[0];
	
			PixbufCache_.flagToPrune ();
			ReferenceCache_.flagToPrune ();
			List<PixbufDiff> diffs = new List<PixbufDiff> ();
			for (int i=0; i<files.Length; ++i) {
				string path = trim (files [i]);
				PixbufDiff diff;
				if (ReferenceStore.equalfiles (files [i])) {
					diff = new PixbufDiff (null, null, files [i]);
				} else {
					Pixbuf A = PixbufCache_.fromCache (path);
					if (A == null) {
						try {
							A = new Pixbuf (path);
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

		static string trim(string path) {
			System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
			return di.Parent.FullName + System.IO.Path.DirectorySeparatorChar + di.Name;
		}
		
		public static void test() {
			string t = trim("/greg//ggregoijh/eee");
			if (t != "/greg/ggregoijh/eee")
				throw new InvalidProgramException();
		}
	}
}

