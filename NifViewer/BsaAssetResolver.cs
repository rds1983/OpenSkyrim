using System;
using System.IO;
using System.Linq;
using AssetManagementBase;

namespace OpenSkyrim.NifViewer
{
	public sealed class BsaAssetResolver : IAssetAccessor
	{
		public BsaAssetResolver()
		{
		}

		public string Name => "BSA";

		public bool Exists(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}

			return SkyrimData.Archives.Values.Any(archive => archive.Contains(path));
		}

		public Stream Open(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new FileNotFoundException("Asset path is empty.", path);
			}

			foreach (var archive in SkyrimData.Archives.Values)
			{
				if (archive.Contains(path))
				{
					return archive.Open(path);
				}
			}

			throw new FileNotFoundException($"Asset '{path}' was not found in any BSA archive registered in SkyrimData.", path);
		}
	}
}