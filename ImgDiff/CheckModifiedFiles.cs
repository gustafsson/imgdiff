using System;
using System.Timers;
using System.Collections.Generic;
using System.IO;

namespace ImgDiff
{
	public class CheckModifiedFiles
	{
		Timer checkmodification_;
		Dictionary<string,DateTime> lastwrites_;

		public string Path {
			get;
			set;
		}

		public delegate void ChangedEventHandler(object sender, FileSystemEventArgs args);
		public event ChangedEventHandler Changed;
		public event ChangedEventHandler Created;
		public event ChangedEventHandler Deleted;

		public CheckModifiedFiles ()
		{
			lastwrites_ = new Dictionary<string, DateTime>();
			
			checkmodification_ = new Timer(500);
			checkmodification_.Elapsed += (sender, e) => checkAgain();

			checkmodification_.Start();
		}

		void checkAgain ()
		{
			String path = Path;
			if (!Directory.Exists(path) || string.IsNullOrWhiteSpace(path))
				return;
			
			string [] files = Directory.GetFiles (new DirectoryInfo(path).FullName);
			
			Dictionary<string,DateTime> thesewrites_ = new Dictionary<string, DateTime> ();
			
			foreach (string file in files) {
				thesewrites_ [file] = Directory.GetLastWriteTimeUtc (file);
			}
			Dictionary<string,DateTime> prevwrites = lastwrites_;
			lastwrites_ = thesewrites_;

			foreach (KeyValuePair<string, DateTime> v in thesewrites_) {
				if (!prevwrites.ContainsKey( v.Key )) {
					if (Created != null)
						Created(this, new FileSystemEventArgs( WatcherChangeTypes.Created, path, System.IO.Path.GetFileName(v.Key) ));
				}
				if (!prevwrites[ v.Key ].Equals( v.Value))
				{
					if (Changed != null)
						Changed(this, new FileSystemEventArgs( WatcherChangeTypes.Changed, path, System.IO.Path.GetFileName(v.Key) ));
				}
			}
			foreach (KeyValuePair<string, DateTime> v in prevwrites) {
				if (!thesewrites_.ContainsKey( v.Key )) {
					if (Deleted != null)
						Deleted(this, new FileSystemEventArgs( WatcherChangeTypes.Changed, path, System.IO.Path.GetFileName(v.Key) ));
				}
			}
		}
	}
}

