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

		public AspectFrame fromCache(string path)
		{
			if (images_.ContainsKey (path)) {
				AspectFrame af = new AspectFrame(null, 0.5f, 0.5f, 1, false);
				Image image = images_ [path];
				image.Data.Remove("to prune");
				Gdk.Pixbuf pixbuf = image.Data["pixbuf"] as Gdk.Pixbuf;
				if (pixbuf != null)
					af.Set( 0.5f, 0.5f, pixbuf.Width/(float)pixbuf.Height, false );
				
				image.Reparent(af);
				
				return af;
			}
			return null;
		}
	}
}

