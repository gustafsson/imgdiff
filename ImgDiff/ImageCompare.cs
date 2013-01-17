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
			double amean = 0;
			double cmean = 0;
			double asum = 0;
			unsafe {
				byte* a = (byte*)A.Pixels;
				byte* b = (byte*)B.Pixels;
				byte* c = (byte*)C.Pixels;
				for (int y=0; y<A.Height; ++y) {
					for (int x=0; x<A.Rowstride; ++x) {
						int o = y * A.Rowstride + x;
						byte d = (byte)Math.Abs (((int)a [o]) - (int)b [o]);
						amean += a [o];
						c [o] = d;
						cmean += d;
						v += d * d;
					}
				}
				amean /= (double)A.Height * A.Rowstride;
				cmean /= (double)A.Height * A.Rowstride;

				double scale = Math.Max(1, 32.0/cmean);
				// Compute R2 denominator sum from 'a', and normalize 'c'
				for (int y=0; y<A.Height; ++y) {
					for (int x=0; x<A.Rowstride; ++x) {
						int o = y * A.Rowstride + x;
						double d = a[o] - amean;
						asum += d*d;
						c[o] = (byte)Math.Min (255.0, c[o]*scale);
					}
				}
			}
			
			r2 = 1 - v/asum;
			// Not actual percent, more like a hint on how much they differ
			diffstring += r2.ToString("G");

			return C;
		}

		public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2) {
			if(a1==null || a2==null || a1.Length!=a2.Length)
				return false;
			fixed (byte* p1=a1, p2=a2) {
				byte* x1=p1, x2=p2;
				int l = a1.Length;
				for (int i=0; i < l/8; i++, x1+=8, x2+=8)
					if (*((long*)x1) != *((long*)x2)) return false;
				if ((l & 4)!=0) { if (*((int*)x1)!=*((int*)x2)) return false; x1+=4; x2+=4; }
				if ((l & 2)!=0) { if (*((short*)x1)!=*((short*)x2)) return false; x1+=2; x2+=2; }
				if ((l & 1)!=0) if (*((byte*)x1) != *((byte*)x2)) return false;
				return true;
			}
		}
	}
}

