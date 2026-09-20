using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetManagementBase;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;

namespace OpenSkyrim.NifViewer
{
	public sealed class ArchiveInfo
	{
		private readonly IArchiveReader _reader;
		private readonly Dictionary<string, IArchiveFile> _fileLookup;
		private readonly List<string> _filePaths;

		public ArchiveInfo(string archivePath)
		{
			ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));

			if (!File.Exists(ArchivePath))
			{
				throw new FileNotFoundException($"The archive file '{ArchivePath}' does not exist.", ArchivePath);
			}

			_reader = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(ArchivePath));

			_fileLookup = new Dictionary<string, IArchiveFile>(StringComparer.OrdinalIgnoreCase);
			_filePaths = new List<string>();

			foreach (var archiveFile in _reader.Files)
			{
				if (string.IsNullOrWhiteSpace(archiveFile.Path))
				{
					continue;
				}

				_filePaths.Add(archiveFile.Path);

				var normalized = NormalizePath(archiveFile.Path);
				if (!string.IsNullOrEmpty(normalized) && !_fileLookup.ContainsKey(normalized))
				{
					_fileLookup.Add(normalized, archiveFile);
				}
			}

			_filePaths.Sort(StringComparer.OrdinalIgnoreCase);
		}

		public string ArchivePath { get; }

		public IReadOnlyList<string> Files => _filePaths;

		public int FileCount => _filePaths.Count;

		public bool Contains(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				return false;
			}

			return _fileLookup.ContainsKey(NormalizePath(filePath));
		}

		public IAssetAccessor CreateAssetResolver()
		{
			return new BsaAssetResolver(this);
		}

		public byte[] Load(string filePath)
		{
			return GetArchiveFile(filePath).GetBytes();
		}

		public Stream Open(string filePath)
		{
			var archiveFile = GetArchiveFile(filePath);
			return new MemoryStream(archiveFile.GetBytes(), writable: false);
		}

		private IArchiveFile GetArchiveFile(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				throw new ArgumentException("The archive file path is required.", nameof(filePath));
			}

			if (!_fileLookup.TryGetValue(NormalizePath(filePath), out var archiveFile))
			{
				throw new FileNotFoundException($"The file '{filePath}' was not found in the archive '{ArchivePath}'.", filePath);
			}

			return archiveFile;
		}

		private static string NormalizePath(string path)
		{
			return path.Replace('\\', '/').TrimStart('/');
		}

		private sealed class BsaAssetResolver : IAssetAccessor
		{
			private readonly ArchiveInfo _owner;

			public BsaAssetResolver(ArchiveInfo owner)
			{
				_owner = owner ?? throw new ArgumentNullException(nameof(owner));
			}

			public string Name => "BSA";

			public bool Exists(string path)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					return false;
				}

				return _owner.Contains(path);
			}

			public Stream Open(string path)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					throw new FileNotFoundException("Asset path is empty.", path);
				}

				return _owner.Open(path);
			}
		}
	}
}