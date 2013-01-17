using System;
using Gdk;

namespace ImgDiff
{
	public class ImageCompare
	{
		public static Pixbuf getDiff (Pixbuf A, Pixbuf B, out string diffstring, out double r2)
		{
			// r2 as in http://en.wikipedia.org/wiki/Coefficient_of_determination
			
			diffstring = "";
			r2 = 0;

			if (A.Width != B.Width || A.Height != B.Height || A.Rowstride != B.Rowstride) {
				B = B.ScaleSimple(A.Width, A.Height, Gdk.InterpType.Nearest);
				diffstring = string.Format ("Different sizes!\n");
			}
			
			//Gdk.Image C = new Gdk.Image (Gdk.ImageType.Normal, Gdk.Visual.Best, A.Width, A.Height);
			Gdk.Pixbuf C = (Gdk.Pixbuf)A.Clone (); //new Gdk.Pixbuf(null, A.Width, A.Height );
			double v = 0;
			double mean = 0;
			double asum = 0;
			unsafe {
				byte* a = (byte*)A.Pixels;
				byte* b = (byte*)B.Pixels;
				byte* c = (byte*)C.Pixels;
				for (int y=0; y<A.Height; ++y) {
					for (int x=0; x<A.Rowstride; ++x) {
						int o = y * A.Rowstride + x;
						int d = Math.Abs (((int)a [o]) - (int)b [o]);
						mean += a [o];
						c [o] = (byte)(d / 2);
						v += d * d;
					}
				}
				mean /= (double)A.Height * A.Width;
				for (int y=0; y<A.Height; ++y) {
					for (int x=0; x<A.Rowstride; ++x) {
						int o = y * A.Rowstride + x;
						double d = a [o] - mean;
						asum += d*d;
					}
				}
			}
			
			r2 = 1 - v/asum;
			// Not actual percent, more like a hint on how much they differ
			diffstring += r2.ToString("G");

			return C;
		}
	}
}

