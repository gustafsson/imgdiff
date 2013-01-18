using System;
using System.Collections.Generic;
using Gtk;

namespace ImgDiff
{
	/**
	 * Speeds up rendering when files doesn't have to be reloaded from disk each time.
	 */
	public class ImageCache
	{
		Dictionary<string,Image> images_;

		public ImageCache ()
		{
			images_ = new Dictionary<string, Image>();
		}

		public void prune ()
		{
			List<string> toprune = new List<string>();
			foreach (KeyValuePair<string,Image> kvp in images_) {
				if (kvp.Value.Data.ContainsKey("to prune"))
					toprune.Add(kvp.Key);
			}
			foreach(string path in toprune)
				images_.Remove(path);
		}

		public void prune(string[]files)
		{
			foreach(string file in files) images_.Remove( file );
		}

		public void flagToPrune()
		{
			foreach (KeyValuePair<string,Image> kvp in images_) {
				kvp.Value.Data["to prune"] = true;
			}
		}

		public void updateCache( Image im )
		{
			images_ [im.Data["path"] as string] = im;
			im.Data.Remove("to prune");
		}

		public Image fromCache(string path)
		{
			if (images_.ContainsKey (path)) {
				Image image = images_ [path];
				image.Data.Remove("to prune");
				Gdk.Pixbuf pixbuf = image.Data["pixbuf"] as Gdk.Pixbuf;

				return image;
			}
			return null;
		}

		// Need an AspectFrame to set aspect ratio.
		public Image createImage (Gdk.Pixbuf pixbuf, string path)
		{
			if (pixbuf!=null)
				System.Console.WriteLine (string.Format("Creating {0}image of {1}",
				                                        pixbuf==null?"empty ":"", 
				                                        System.IO.Path.GetFileName(path)));
			
			Image img = new Image ();
			img.Name = null;
			img.Pixbuf = pixbuf;
			img.Data["pixbuf"] = pixbuf;
			img.Data["path"] = path;
			img.SetSizeRequest( 0, 0 );

			if (pixbuf != null)
			{
				img.SizeAllocated += (o, args) =>
				{
					Image im = (o as Image);
					Gdk.Pixbuf pb = (im.Data["pixbuf"] as Gdk.Pixbuf);
					
					if (im.Pixbuf.Width != args.Allocation.Width || im.Pixbuf.Height != args.Allocation.Height)
					{
						im.Pixbuf = pb.ScaleSimple(args.Allocation.Width, args.Allocation.Height, Gdk.InterpType.Nearest);
					}
				};
			}
			
			return img;
		}
	}
}

