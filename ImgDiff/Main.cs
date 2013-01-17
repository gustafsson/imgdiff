using System;
using Gtk;

namespace ImgDiff
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			imgdiff win = new imgdiff ();
			win.Show ();
			Application.Run ();
		}
	}
}
