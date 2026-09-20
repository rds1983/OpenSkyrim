using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSkyrim.NifViewer;

public class SkyrimFileSystem
{
	private class ArchiveFileInfo
	{
		public string ArchivePath { get; }
		public IArchiveFile File { get; }

		public ArchiveFileInfo(string archivePath, IArchiveFile file)
		{
			ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
			File = file ?? throw new ArgumentNullException(nameof(file));
		}

		public override string ToString() => $"{Path.GetFileName(ArchivePath)}, {File.Path}";
	}

	private readonly Dictionary<string, ArchiveFileInfo> _files = new Dictionary<string, ArchiveFileInfo>();

	public string RootPath { get; }
	public string DataPath { get; }
	public string[] Keys { get; private set; }


	public SkyrimFileSystem(string rootPath)
	{
		OSK.LogInfo($"Using Skyrim Folder '{rootPath}'");
		RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
		DataPath = ResolveSkyrimDataFolder(RootPath);
		ParseArchives();
	}

	public Stream Open(string key)
	{
		ArchiveFileInfo fileInfo;
		if (!_files.TryGetValue(key, out fileInfo))
		{
			throw new Exception($"Unknown file '{key}'");
		}

		return fileInfo.File.AsStream();
	}

	private static string ResolveSkyrimDataFolder(string path)
	{
		if (string.Equals(Path.GetFileName(path), "Data", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
		{
			return path;
		}

		var dataDirectory = Path.Combine(path, "Data");
		if (Directory.Exists(dataDirectory))
		{
			return dataDirectory;
		}

		throw new Exception($"Unable to find 'Data' folder at '{path}'");
	}

	private void ParseArchives()
	{
		var archives = new List<ArchiveInfo>();
		foreach (var archivePath in Directory.EnumerateFiles(DataPath, "*.bsa", SearchOption.AllDirectories))
		{
			OSK.LogInfo($"Parsing '{archivePath}'...");
			var reader = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(archivePath));

			var count = 0;
			foreach (var archiveFile in reader.Files)
			{
				if (string.IsNullOrWhiteSpace(archiveFile.Path))
				{
					continue;
				}

				var newFile = new ArchiveFileInfo(archivePath, archiveFile);

				ArchiveFileInfo oldFile;
				if (_files.TryGetValue(archiveFile.Path, out oldFile))
				{
					OSK.LogWarning($"Dublicate file '{archiveFile.Path}': old = '{Path.GetFileName(oldFile.ArchivePath)}', new = '{Path.GetFileName(newFile.ArchivePath)}'.");
				}

				_files[archiveFile.Path] = newFile;
				++count;
			}

			OSK.LogInfo($"Found {count} files.");
		}

		Keys = (from k in _files.Keys orderby k select k).ToArray();

		OSK.LogInfo($"Total files: {_files.Count}.");
	}
}
