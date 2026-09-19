# Mutagen Notes

This project uses Mutagen to inspect Skyrim plugin and archive data in a way that matches Bethesda's real install layout.

## Key findings

- Skyrim assets are not primarily loose `.nif` files under the `Data` directory.
- Most model content is stored inside `.bsa` archives.
- Mutagen must be used to enumerate the archives and plugin files rather than raw filesystem scanning alone.
- The project uses the `0.52.0` Mutagen package family, which includes the archive APIs needed for BSA access.

## Working pattern

### 1. Resolve the Skyrim install root

Use the game install folder and then resolve the `Data` directory:

- default install: `D:\SteamLibrary\steamapps\common\Skyrim Special Edition`
- data folder: `...\Skyrim Special Edition\Data`

This logic should normalize both a root folder and a direct `Data` path.

### 2. Use Mutagen for Skyrim context

The valid game constants used by this project are exposed through:

- `Mutagen.Bethesda.Plugins.Meta.GameConstants`
- `GameConstants.SkyrimSE`

This confirms the project is targeting the Skyrim SE data layout.

### 3. Enumerate `.bsa` archives with the public archive API

The important part is that `BsaReader` is internal in the Mutagen `0.52.0` package. The supported public entry point is:

- `Mutagen.Bethesda.Archives.Archive.CreateReader(GameRelease release, FilePath path)`

Then enumerate the archive entries through:

- `archive.Files`
- check each `file.Path`
- keep entries whose extension is `.nif`

This is the correct approach for discovering model files that are packed inside Bethesda archives.

### 4. Keep plugin files in scope

The project also recognizes `.esm`, `.esp`, and `.esl` files under the data directory as part of the Skyrim mod/plugin set. They are relevant for context, even if the actual `.nif` files may still be inside archives.

## Recommended implementation pattern

```csharp
var dataFolder = SkyrimDataScanner.ResolveSkyrimDataFolder(skyrimFolder);
var nifFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

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
```

## Important caveat

Do not assume `.nif` files are all loose under `Data`. In a real Skyrim installation, a large portion of assets will only be discoverable through archive parsing. The final scanner should combine:

- raw loose-file scanning for local files
- Mutagen archive scanning for `.bsa` contents
- plugin awareness for `.esm` / `.esp` / `.esl` files

## Package versions used

- `Mutagen.Bethesda.Core` version `0.52.0`
- `Mutagen.Bethesda.Skyrim` version `0.52.0`

These versions exposed the needed public archive and game constants needed for this project.
