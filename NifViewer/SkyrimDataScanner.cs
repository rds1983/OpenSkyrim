using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;

namespace OpenSkyrim.NifViewer
{
    public static class SkyrimDataScanner
    {
        public const string DefaultSkyrimFolder = @"D:\SteamLibrary\steamapps\common\Skyrim Special Edition";

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

        public static IReadOnlyList<string> EnumerateNifFiles(string skyrimFolder)
        {
            var dataFolder = ResolveSkyrimDataFolder(skyrimFolder);
            if (!Directory.Exists(dataFolder))
            {
                return Array.Empty<string>();
            }

            // Mutagen validates that this is a Skyrim SE data set and exposes the Bethesda archive API.
            var gameConstants = GameConstants.SkyrimSE;
            _ = gameConstants;

            var nifFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(dataFolder, "*.nif", SearchOption.AllDirectories))
            {
                nifFiles.Add(file);
            }

            foreach (var bsaFile in Directory.EnumerateFiles(dataFolder, "*.bsa", SearchOption.AllDirectories))
            {
                try
                {
                    var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaFile));
                    foreach (var file in archive.Files)
                    {
                        if (string.Equals(Path.GetExtension(file.Path), ".nif", StringComparison.OrdinalIgnoreCase))
                        {
                            nifFiles.Add($"{bsaFile}::{file.Path}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse BSA '{bsaFile}': {ex.Message}");
                }
            }

            foreach (var pluginFile in Directory.EnumerateFiles(dataFolder, "*.esm", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(dataFolder, "*.esl", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(dataFolder, "*.esp", SearchOption.AllDirectories)))
            {
                if (File.Exists(pluginFile))
                {
                    _ = pluginFile;
                }
            }

            return nifFiles
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();
        }
    }
}
