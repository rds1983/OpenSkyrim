using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSkyrim.NifViewer
{
	public static class SkyrimData
	{
		public const string DefaultSkyrimFolder = @"D:\SteamLibrary\steamapps\common\Skyrim Special Edition";

		private static readonly object SyncRoot = new object();
		private static readonly Dictionary<string, ArchiveInfo> _archives = new Dictionary<string, ArchiveInfo>(StringComparer.OrdinalIgnoreCase);

		public static IReadOnlyDictionary<string, ArchiveInfo> Archives
		{
			get
			{
				lock (SyncRoot)
				{
					return new Dictionary<string, ArchiveInfo>(_archives, StringComparer.OrdinalIgnoreCase);
				}
			}
		}

		public static string ResolveSkyrimDataFolder(string skyrimFolder)
		{
			var candidate = string.IsNullOrWhiteSpace(skyrimFolder)
				? DefaultSkyrimFolder
				: skyrimFolder.Trim();

			if (string.IsNullOrWhiteSpace(candidate))
			{
				return DefaultSkyrimFolder;
			}

			candidate = Path.GetFullPath(candidate);

			if (string.Equals(Path.GetFileName(candidate), "Data", StringComparison.OrdinalIgnoreCase) && Directory.Exists(candidate))
			{
				return candidate;
			}

			var dataDirectory = Path.Combine(candidate, "Data");
			if (Directory.Exists(dataDirectory))
			{
				return dataDirectory;
			}

			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			return dataDirectory;
		}

		public static string ResolveSkyrimGameRoot(string skyrimFolder)
		{
			var dataFolder = ResolveSkyrimDataFolder(skyrimFolder);
			var gameRoot = Directory.Exists(dataFolder)
				? Path.GetDirectoryName(dataFolder)
				: Path.GetDirectoryName(DefaultSkyrimFolder);

			if (string.IsNullOrWhiteSpace(gameRoot))
			{
				return DefaultSkyrimFolder;
			}

			return gameRoot;
		}

		public static void Initialize(string skyrimPath)
		{
			var dataFolder = ResolveSkyrimDataFolder(skyrimPath);

			lock (SyncRoot)
			{
				_archives.Clear();
			}

			if (!Directory.Exists(dataFolder))
			{
				return;
			}

			foreach (var bsaPath in Directory.EnumerateFiles(dataFolder, "*.bsa", SearchOption.AllDirectories))
			{
				try
				{
					Register(bsaPath);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Failed to parse BSA '{bsaPath}': {ex.Message}");
				}
			}
		}

		private static void Register(string archivePath)
		{
			if (string.IsNullOrWhiteSpace(archivePath))
			{
				throw new ArgumentException("The archive path is required.", nameof(archivePath));
			}

			var archive = new ArchiveInfo(archivePath);
			var key = Path.GetFileName(archive.ArchivePath);

			lock (SyncRoot)
			{
				_archives[key] = archive;
			}
		}

		public static ArchiveInfo Get(string fileName)
		{
			if (!TryGet(fileName, out var archiveInfo))
			{
				throw new KeyNotFoundException($"No archive registered with the file name '{fileName}'.");
			}

			return archiveInfo;
		}

		public static bool TryGet(string fileName, out ArchiveInfo archiveInfo)
		{
			if (string.IsNullOrWhiteSpace(fileName))
			{
				archiveInfo = null;
				return false;
			}

			lock (SyncRoot)
			{
				return _archives.TryGetValue(fileName, out archiveInfo);
			}
		}

		public static bool Contains(string fileName)
		{
			return TryGet(fileName, out _);
		}
	}
}