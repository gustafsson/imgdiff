using System;
using Gtk;

namespace ImgDiff
{
	class MainClass
	{
		static void test() {
			DiffComputer.test();
		}

		public static void Main (string[] args)
		{
			test ();

			Application.Init ();
			imgdiff win = new imgdiff ();
			win.Show ();
			Application.Run ();
		}
	}
}
