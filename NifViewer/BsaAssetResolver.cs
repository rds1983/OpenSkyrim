using System;
using System.IO;
using System.Linq;
using AssetManagementBase;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;

namespace OpenSkyrim.NifViewer
{
	public sealed class BsaAssetResolver : IAssetAccessor
	{
		private readonly string _archivePath;

		public BsaAssetResolver(string archivePath)
		{
			_archivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
		}

		public string Name => "BSA";

		public bool Exists(string path)
		{
			var normalized = NormalizePath(path);
			if (string.IsNullOrEmpty(normalized))
			{
				return false;
			}

			try
			{
				var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(_archivePath));
				return archive.Files.Any(f => string.Equals(NormalizePath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));
			}
			catch
			{
				return false;
			}
		}

		public Stream Open(string path)
		{
			var normalized = NormalizePath(path);
			if (string.IsNullOrEmpty(normalized))
			{
				throw new FileNotFoundException("Asset path is empty.", path);
			}

			var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(_archivePath));
			var archiveFile = archive.Files.FirstOrDefault(f => string.Equals(NormalizePath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));
			if (archiveFile == null)
			{
				throw new FileNotFoundException($"Asset '{path}' was not found in BSA '{_archivePath}'.", path);
			}

			var bytes = archiveFile.GetBytes();
			return new MemoryStream(bytes, writable: false);
		}

		private static string NormalizePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return string.Empty;
			}

			return path.Replace('\\', '/').TrimStart('/');
		}
	}
}
