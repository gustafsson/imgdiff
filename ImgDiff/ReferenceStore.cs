using System;
using System.IO;
using Gtk;
using System.IO.IsolatedStorage;

namespace ImgDiff
{
	public class ReferenceStore
	{
		public static void validateImage( string path )
		{
			string isopath = ReferenceStore.isopath (path);
			
			using (IsolatedStorageFile isoStore = ReferenceStore.isoStore()) {
				// copy file int o isolated storage
				using (IsolatedStorageFileStream output = isoStore.OpenFile(isopath, FileMode.Create)) {
					using (FileStream input = new FileStream(path, FileMode.Open)) {
						CopyStream (input, output);
					}
				}
			}
		}

		public static Gdk.Pixbuf getReferenceImage (string path)
		{
			Gdk.Pixbuf pixbuf = null;
			string isopath = ReferenceStore.isopath(path);
			using (IsolatedStorageFile isoStore = ReferenceStore.isoStore()) {
				if (isoStore.FileExists (isopath)) {
					using (IsolatedStorageFileStream isf = isoStore.OpenFile (isopath, FileMode.Open)) {
						pixbuf = new Gdk.Pixbuf (isf);
					}
				}
			}
			return pixbuf;
		}

		public static bool equalfiles (string path)
		{
			using (IsolatedStorageFile isoStore = ReferenceStore.isoStore()) {
				string isopath = ReferenceStore.isopath(path);
				if (!isoStore.FileExists (isopath))
					return false;
				
				using (IsolatedStorageFileStream input1 = isoStore.OpenFile(isopath, FileMode.Open)) {
					using (FileStream input2 = new FileStream(path, FileMode.Open)) {
						byte[] buffer1 = new byte[32768];
						byte[] buffer2 = new byte[32768];
						int read1, read2;
						while (true) {
							read1 = input1.Read (buffer1, 0, buffer1.Length);
							read2 = input2.Read (buffer2, 0, buffer2.Length);
							if (read1 != read2)
								return false;
							if (read1 <= 0)
								break;
							//if (!Array.Equals(buffer1, buffer2))
							//    return false;
							if (!PixbufDiff.UnsafeCompare(buffer1, buffer2))
								return false;
						}
					}
				}
			}
			
			return true;
		}

		static IsolatedStorageFile isoStore ()
		{
			return IsolatedStorageFile.GetUserStoreForAssembly();
		}

		static string isopath(string path)
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

		static void CopyStream(Stream input, Stream output)
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

