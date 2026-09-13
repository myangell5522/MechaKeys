using System.IO;

namespace MechaKeys.Core
{
	internal sealed class SoundClip
	{
		public string FilePath;
		public string ZipPath;
		public string ZipEntry;

		public string CacheKey => ZipPath == null ? FilePath : ZipPath + "|" + ZipEntry;

		public string Extension
		{
			get
			{
				string name = ZipEntry ?? FilePath ?? "";
				return Path.GetExtension(name).ToLowerInvariant();
			}
		}
	}
}
