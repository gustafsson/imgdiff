using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.IO;
using System.IO.IsolatedStorage;

namespace ImgDiff
{
	public class MySettings:ISerializable
	{
		public string Path
		{
			get;
			set;
		}       
		public double R2_Threshold
		{
			get;
			set;
		}
		public MySettings()
		{
			this.Path = "";
			this.R2_Threshold = 0.9999;
		}
		public MySettings(string Path, double R2_Threshold)
		{
			this.Path = Path;
			this.R2_Threshold = R2_Threshold;
		}
		void ISerializable.GetObjectData(SerializationInfo oInfo, StreamingContext oContext)
		{
			oInfo.AddValue("Path", this.Path);
			oInfo.AddValue("R2_Threshold", this.R2_Threshold);
		}

		public void save ()
		{
			using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForAssembly())
			{
				XmlSerializer oSerialiser = new XmlSerializer(typeof(MySettings));
				using (IsolatedStorageFileStream fs = isf.OpenFile("mysettings.xml", FileMode.Create))
				{
					oSerialiser.Serialize(fs, this);
				}
			}
		}
		public static MySettings load ()
		{
			try {
				using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForAssembly()) {
					XmlSerializer oSerialiser = new XmlSerializer (typeof(MySettings));
					using (IsolatedStorageFileStream fs = isf.OpenFile("mysettings.xml", FileMode.Open)) {
						return oSerialiser.Deserialize(fs) as MySettings;
					}
				}
			} catch (IOException) {
				// Return default values
				return new MySettings();
			}
		}
	}
}

