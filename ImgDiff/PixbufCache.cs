using System;
using System.Collections.Generic;
using Gdk;

namespace ImgDiff
{
	/**
	 * Speeds up rendering when files doesn't have to be reloaded from disk each time.
	 */
	public class PixbufCache
	{
		Dictionary<string,Pixbuf> pixbufs_;

		public PixbufCache ()
		{
			pixbufs_ = new Dictionary<string, Pixbuf>();
		}

		public void prune ()
		{
			List<string> toprune = new List<string>();
			foreach (KeyValuePair<string,Pixbuf> kvp in pixbufs_) {
				if (kvp.Value.Data.ContainsKey("to prune"))
					toprune.Add(kvp.Key);
			}
			foreach(string path in toprune)
				pixbufs_.Remove(path);
		}

		public void prune(string[]files)
		{
			foreach(string file in files) pixbufs_.Remove( file );
		}

		public void flagToPrune()
		{
			foreach (KeyValuePair<string,Pixbuf> kvp in pixbufs_) {
				kvp.Value.Data["to prune"] = true;
			}
		}

		public void updateCache( string path, Pixbuf im )
		{
			pixbufs_ [path] = im;
			im.Data.Remove("to prune");
		}

		public Pixbuf fromCache(string path)
		{
			if (pixbufs_.ContainsKey (path)) {
				Pixbuf pixbuf = pixbufs_ [path];
				pixbuf.Data.Remove("to prune");
				return pixbuf;
			}
			return null;
		}

		// Need an AspectFrame to set aspect ratio.
		static public Gtk.Image createImage (Pixbuf pixbuf)
		{
			Gtk.Image img = new Gtk.Image ();
			img.Name = null;
			img.Pixbuf = pixbuf;
			img.Data["pixbuf"] = pixbuf;
			img.SetSizeRequest( 0, 0 );

			if (pixbuf != null)
			{
				img.SizeAllocated += (o, args) =>
				{
					Gtk.Image im = (o as Gtk.Image);
					Pixbuf pb = (im.Data["pixbuf"] as Pixbuf);
					
					if (im.Pixbuf.Width != args.Allocation.Width || im.Pixbuf.Height != args.Allocation.Height)
					{
						im.Pixbuf = pb.ScaleSimple(args.Allocation.Width, args.Allocation.Height, InterpType.Nearest);
					}
				};
			}
			
			return img;
		}
	}
}

