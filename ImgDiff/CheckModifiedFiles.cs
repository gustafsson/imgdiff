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

		public delegate void ChangedEventHandler(object sender, string[] file);
		public event ChangedEventHandler FoundModifiedFile;

		public CheckModifiedFiles ()
		{
			lastwrites_ = new Dictionary<string, DateTime>();
			
			checkmodification_ = new Timer(500);
			checkmodification_.Elapsed += (sender, e) => checkAgain();

			checkmodification_.Start();
		}

		void checkAgain ()
		{
			if (!Directory.Exists(Path) || string.IsNullOrWhiteSpace(Path))
				return;
			
		    string [] files = Directory.GetFiles (Path);
			
			Dictionary<string,DateTime> thesewrites_ = new Dictionary<string, DateTime> ();
			
			foreach (string file in files) {
				thesewrites_ [file] = Directory.GetLastWriteTimeUtc (file);
			}
			Dictionary<string,DateTime> prevwrites = lastwrites_;
			lastwrites_ = thesewrites_;

			List<string> modifiedFiles = new List<string>();
			foreach (KeyValuePair<string, DateTime> v in thesewrites_) {
				if (!prevwrites.ContainsKey( v.Key )) // filesystemwatcher handles this
					return;
				if (!prevwrites[ v.Key ].Equals( v.Value))
				{
					modifiedFiles.Add( v.Key );
				}
			}
			if (modifiedFiles.Count > 0)
				if (FoundModifiedFile != null)
					FoundModifiedFile( this, modifiedFiles.ToArray() );
		}
	}
}

