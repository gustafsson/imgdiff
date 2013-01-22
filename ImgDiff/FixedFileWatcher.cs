using System;
using System.IO;

namespace ImgDiff
{
	/**
	 * FileSystemWatcher.Changed doesn't work on Os X. This class implements it
	 * using CheckModifiedFiles whichs polls the disk for timestamps.
	 */
	public class FixedFileWatcher
	{
		CheckModifiedFiles CheckModifiedFiles_;
		FileSystemWatcher watcher_;
		string Path_;

		public string Path {
			get {
				return Path_;
			}
			set {
				Path_ = value;

				if (CheckModifiedFiles_ != null)
					CheckModifiedFiles_.Path = value;

				if (watcher_ != null)
				{
					if (Directory.Exists (value)) {
						watcher_.Path = value;
						watcher_.EnableRaisingEvents = true;
					} else
						watcher_.EnableRaisingEvents = false;
				}
			}
		}

		public delegate void ChangedEventHandler(object sender, string affected_file);

		/**
		 * This event will be fired from various random different threads.
		 * Remember to dispatche the call to your GUI thread if needed.
		 * 
		 * Like so:
		 * 
		 * FixedFileWatcher ffw = new FixedFileWatcher();
		 * ffw.Changed += (s,f) {
		 *     Gtk.Application.Invoke( delegate {
		 *	       MyCall();
		 *     });
		 * };
		 */
		public event ChangedEventHandler Changed;

		public FixedFileWatcher ()
		{
			bool useFileSystemWatcher = false;
			if (useFileSystemWatcher) {
				watcher_ = new FileSystemWatcher ();
				watcher_.Changed += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
				watcher_.Created += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
				watcher_.Deleted += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
				watcher_.Renamed += (object sender, RenamedEventArgs e) => WatcherUpdate (e);
			} else {
				CheckModifiedFiles_ = new CheckModifiedFiles ();
				CheckModifiedFiles_.Changed += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
				CheckModifiedFiles_.Created += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
				CheckModifiedFiles_.Deleted += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
			}
		}

		void WatcherUpdate (FileSystemEventArgs e)
		{
			if (Changed != null)
				Changed(this, e.FullPath);
		}
	}
}

