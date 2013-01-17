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

		public string Path {
			get {
				return CheckModifiedFiles_.Path;
			}
			set {
				CheckModifiedFiles_.Path = value;
				if (Directory.Exists (value)) {
					watcher_ = value;
					watcher_.EnableRaisingEvents = true;
				} else
					watcher_.EnableRaisingEvents = false;
			}
		}

		public delegate void ChangedEventHandler(object sender, string[] affected_files);

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
			CheckModifiedFiles_ = new CheckModifiedFiles();
			CheckModifiedFiles_.FoundModifiedFile += (object sender, string[] files) => {
				if (Changed != null)
					Changed(this, files);
			};

			this.watcher_ = new FileSystemWatcher ();
			watcher_.Changed += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Created += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Deleted += (object sender, FileSystemEventArgs e) => WatcherUpdate (e);
			watcher_.Renamed += (object sender, RenamedEventArgs e) => WatcherUpdate (e);
		}

		void WatcherUpdate (FileSystemEventArgs e)
		{
			if (Changed != null)
				Changed(this, new string[]{e.FullPath});
		}
	}
}

