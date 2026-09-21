using DigitalRiseModel;
using Microsoft.Xna.Framework.Graphics;
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

		public object Value { get; set; }

		public ArchiveFileInfo(string archivePath, IArchiveFile file)
		{
			ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
			File = file ?? throw new ArgumentNullException(nameof(file));
		}

		public override string ToString() => $"{Path.GetFileName(ArchivePath)}, {File.Path}";
	}

	private readonly Dictionary<string, ArchiveFileInfo> _files = new Dictionary<string, ArchiveFileInfo>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, object> _looseCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

	private static readonly string[] AssetPrefixes =
	{
		"meshes/", "textures/", "sound/", "music/", "interface/", "shaders/", "scripts/", "materials/"
	};

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
		if (TryGetArchiveFile(key, out var fileInfo))
		{
			return fileInfo.File.AsStream();
		}

		var loosePath = GetLoosePath(key);
		if (loosePath != null)
		{
			return File.OpenRead(loosePath);
		}

		throw new Exception($"Unknown file '{key}'");
	}

	public bool FileExists(string key) => TryGetArchiveFile(key, out _) || GetLoosePath(key) != null;

	public string GetPluginPath(string fileName) => Path.Combine(DataPath, fileName);

	public Texture2D LoadTexture(GraphicsDevice graphicsDevice, string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return null;
		}

		if (TryGetCached(key, out Texture2D cached))
		{
			return cached;
		}

		try
		{
			using var stream = Open(key);
			var texture = Texture2D.DDSFromStreamEXT(graphicsDevice, stream);
			texture.Name = key;
			OSK.LogInfo($"Loaded texture '{key}'");
			SetCached(key, texture);
			return texture;
		}
		catch (Exception ex)
		{
			OSK.LogWarning($"Failed to load texture '{key}': {ex.Message}");
			return null;
		}
	}

	public DrModel LoadModel(GraphicsDevice graphicsDevice, string key)
	{
		if (TryGetCached(key, out DrModel cached))
		{
			return cached;
		}

		using var stream = Open(key);
		var model = NifModelLoader.LoadDrModel(graphicsDevice, stream, Path.GetFileNameWithoutExtension(key), this);
		SetCached(key, model);
		return model;
	}

	private bool TryGetCached<T>(string key, out T value) where T : class
	{
		if (TryGetArchiveFile(key, out var fileInfo))
		{
			value = fileInfo.Value as T;
			return value != null;
		}

		if (_looseCache.TryGetValue(NormalizePath(key), out var looseValue))
		{
			value = looseValue as T;
			return value != null;
		}

		value = null;
		return false;
	}

	private void SetCached(string key, object value)
	{
		if (TryGetArchiveFile(key, out var fileInfo))
		{
			fileInfo.Value = value;
			return;
		}

		_looseCache[NormalizePath(key)] = value;
	}

	private bool TryGetArchiveFile(string key, out ArchiveFileInfo fileInfo)
	{
		var normalized = NormalizePath(key);
		if (_files.TryGetValue(normalized, out fileInfo))
		{
			return true;
		}

		foreach (var prefix in AssetPrefixes)
		{
			if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
				_files.TryGetValue(prefix + normalized, out fileInfo))
			{
				return true;
			}
		}

		fileInfo = null;
		return false;
	}

	private string GetLoosePath(string key)
	{
		var normalized = NormalizePath(key);

		var candidate = Path.Combine(DataPath, normalized.Replace('/', Path.DirectorySeparatorChar));
		if (File.Exists(candidate))
		{
			return candidate;
		}

		foreach (var prefix in AssetPrefixes)
		{
			if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			candidate = Path.Combine(DataPath, (prefix + normalized).Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private static string NormalizePath(string path) => path.Replace('\\', '/');

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

				var path = NormalizePath(archiveFile.Path);
				var newFile = new ArchiveFileInfo(archivePath, archiveFile);

				ArchiveFileInfo oldFile;
				if (_files.TryGetValue(path, out oldFile))
				{
					OSK.LogWarning($"Dublicate file '{path}': old = '{Path.GetFileName(oldFile.ArchivePath)}', new = '{Path.GetFileName(newFile.ArchivePath)}'.");
				}

				_files[path] = newFile;
				++count;
			}

			OSK.LogInfo($"Found {count} files.");
		}

		Keys = (from k in _files.Keys orderby k select k).ToArray();

		OSK.LogInfo($"Total files: {_files.Count}.");
	}
}
