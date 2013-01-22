using System;
using Gdk;

namespace ImgDiff
{
	public class PixbufDiff
	{
		public string Path {
			get;
			private set;
		}
		public Pixbuf A {
			get;
			private set;
		}
		public Pixbuf B {
			get;
			private set;
		}
		public Pixbuf Diff {
			get;
			private set;
		}
		public double R2 {
			get;
			private set;
		}
		public string diffstring {
			get;
			private set;
		}


		public PixbufDiff (Pixbuf A, Pixbuf B, string path)
		{
			this.Path = path;
			this.A = A;
			this.B = B;

			if (A == null) {
				this.Diff = null;
				this.R2 = 1;
				this.diffstring = "";
			} else if (B == null) {
				this.Diff = null;
				this.R2 = 0;
				this.diffstring = "";
			} else {
				string diffstring_;
				double R2_;
				this.Diff = PixbufDiff.getDiff (A, B, out diffstring_, out R2_);
				this.R2 = R2_;
				this.diffstring = diffstring_;
			}
		}


		static Pixbuf getDiff (Pixbuf A, Pixbuf B, out string diffstring, out double r2)
		{
			// r2 as in http://en.wikipedia.org/wiki/Coefficient_of_determination
			
			diffstring = "";
			r2 = 0;

			if (A.Width != B.Width || A.Height != B.Height) {
				B = B.ScaleSimple (A.Width, A.Height, Gdk.InterpType.Nearest);
				diffstring = string.Format ("Different sizes.\n");
			}
			if (A.HasAlpha != B.HasAlpha) {
				if (A.HasAlpha && !B.HasAlpha)
					diffstring += string.Format ("Reference image doesn't have an alpha channel. But new the image does.\n");
				else
					diffstring += string.Format ("Reference image has an alpha channel. But new the image doesn't.\n");
			} else {
				if (A.Rowstride != B.Rowstride) {
					diffstring += string.Format ("Different rowstride.\n");
				}
			}
			if (A.NChannels - (A.HasAlpha ? 1 : 0) != B.NChannels - (B.HasAlpha ? 1 : 0)) {
				diffstring += string.Format ("Different number of channels.");
				return null;
			}

			if (A.BitsPerSample != B.BitsPerSample) {
				diffstring += string.Format ("Different bits per sample\n");
				return null;
			}

			if (A.BitsPerSample != 8) {
				diffstring += string.Format ("Only support diffs of images with 8 bits per sample, got {1}.", A.BitsPerSample);
				return null;
			}
			Gdk.Pixbuf C = (Gdk.Pixbuf)A.Clone ();
			double v = 0;
			double amean = 0;
			double cmean = 0;
			double asum = 0;
			unsafe {
				byte* a = (byte*)A.Pixels;
				byte* b = (byte*)B.Pixels;
				byte* c = (byte*)C.Pixels;
				int row = Math.Min(Math.Min(A.Rowstride, B.Rowstride), C.Rowstride);
				for (int y=0; y<A.Height; ++y) {
					int oa = y * A.Rowstride;
					int ob = y * B.Rowstride;
					int oc = y * C.Rowstride;
					for (int n=0; n<A.Width; ++n)
					{
						for (int m=0; m<A.NChannels; ++m) {
							int bv = 0;
							if (m < B.NChannels)
								bv = (int)b [ob++];
							byte d = (byte)Math.Abs (((int)a [oa]) - bv);
							amean += a [oa++];
							c [oc++] = d;
							cmean += d;
							v += d * d;
						}
					}
				}
				amean /= (double)A.Height * A.Width * A.NChannels;
				cmean /= (double)C.Height * C.Width * C.NChannels;

				double scale = Math.Max(1, 32.0/cmean);
				// Compute R2 denominator sum from 'a', and normalize 'c'
				for (int y=0; y<A.Height; ++y) {
					int oa = y * A.Rowstride;
					int oc = y * C.Rowstride;
					for (int x=0; x<row; ++x) {
						double d = a[oa+x] - amean;
						asum += d*d;
						c[oc+x] = (byte)Math.Min (255.0, c[oc+x]*scale);
					}
				}
			}
			
			r2 = 1 - v/asum;
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

