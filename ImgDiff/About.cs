using System;
using System.IO.IsolatedStorage;

namespace ImgDiff
{
	public partial class About : Gtk.Dialog
	{
		public About ()
		{
			this.Build ();

			recomputeSize();
		}

		protected void OnButton9Clicked (object sender, EventArgs e)
		{
			// Clear isolated storage
			//
			using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetUserStoreForAssembly()) {
				foreach(string file in isoStore.GetFileNames())
					isoStore.DeleteFile(file);
			}

			recomputeSize();
		}

		void recomputeSize ()
		{
			using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetUserStoreForAssembly()) {
				double usedGb = Math.Round( isoStore.UsedSize / 1024.0 / 1024.0 / 1024.0*100)/100;
				double quota = Math.Round( isoStore.Quota / 1024.0 / 1024.0 / 1024.0*100)/100;
				this.labelSpace.Text = string.Format("{0} GB of a quota of", usedGb);
				this.entry1.Text = string.Format("{0}", quota);
			}
		}

		protected void OnEntry1EditingDone (object sender, EventArgs e)
		{
			using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetUserStoreForAssembly()) {
				double quota = Math.Round( isoStore.Quota / 1024.0 / 1024.0 / 1024.0*100)/100;
				double.TryParse( this.entry1.Text, out quota );
				isoStore.IncreaseQuotaTo((long)(quota*1024*1024*1024));
			}
			recomputeSize();
		}

		protected void OnButtonOkClicked (object sender, EventArgs e)
		{
			Destroy();
		}
	}
}

