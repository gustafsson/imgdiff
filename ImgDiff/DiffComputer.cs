using System;
using System.Diagnostics;
using System.IO;
using Gdk;
using System.Collections.Generic;
using System.Timers;

namespace ImgDiff
{
	public class DiffComputer
	{
		object DiffListLock_ = new object();

		public List<PixbufDiff> DiffList {
			get {
				lock (DiffListLock_) {
					return new List<PixbufDiff> (DiffList_);
				}
			}
			private set {
				lock (DiffListLock_) {
					DiffList_ = value;
				}
			}
		}

		public string Path {
			get {
				return FixedFileWatcher_.Path;
			}
			set {
				FixedFileWatcher_.Path = value;
				recopmuteDiff();
			}
		}

		public delegate void ChangedEventHandler(DiffComputer sender);
		public event ChangedEventHandler DiffListChanged;

		FixedFileWatcher FixedFileWatcher_;
		PixbufCache PixbufCache_;
		PixbufCache ReferenceCache_;
		List<PixbufDiff> DiffList_;

		public DiffComputer ()
		{
			PixbufCache_ = new PixbufCache();
			ReferenceCache_ = new PixbufCache();
			
			FixedFileWatcher_ = new FixedFileWatcher();
			FixedFileWatcher_.Changed += (object sender, string[] files) => {
				PixbufCache_.prune(files);
				recopmuteDiff();
			};

			recopmuteDiff();
		}

		void recopmuteDiff()
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
				string path = files[i];
				PixbufDiff diff;
				if (ReferenceStore.equalfiles (files [i]))
				{
					diff = new PixbufDiff(null, null, files[i]);
				}
				else
				{
					Pixbuf A = PixbufCache_.fromCache (path);
					if (A == null) {
						try
						{
							A = new Pixbuf (path);
							PixbufCache_.updateCache (path, A);
						} catch (GLib.GException x) {
							if (x.Source == "gdk-sharp") {
								System.Console.WriteLine (x.Message);
								// Not an image file
							} else {
								System.Console.WriteLine (x.Message);
							}
							continue;
						} catch (Exception x) {
							if (x.Source == "gdk-sharp") {
								System.Console.WriteLine (x.Message);
								// Not an image file
							} else {
								System.Console.WriteLine (x.Message);
							}
							continue;
						}
					}
					
					Pixbuf B = ReferenceCache_.fromCache (path);
					if (B == null) {
						try
						{
							B = ReferenceStore.getReferenceImage (path);
							ReferenceCache_.updateCache (path, B);
						} catch (Exception x) {
							B = imgdiff.ErrorPixbuf;
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

			this.DiffList = diffs;

			if (DiffListChanged != null)
				DiffListChanged(this);
		}
	}
}

