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

#### Asset path prefixes (important)

Archive entries are stored **with** their asset-root prefix, e.g.
`meshes\dungeons\nordic\...\NorRmBgMid02.nif` or `textures\...`. However, plugin
records store model/texture paths **without** that prefix
(e.g. `Dungeons\Nordic\...\NorRmBgMid02.nif`). A naive exact-match lookup therefore
fails for every record-derived path.

`SkyrimFileSystem` must try the path as-is and also with each standard prefix
(`meshes/`, `textures/`, `sound/`, `music/`, `interface/`, `shaders/`, `scripts/`,
`materials/`). Loose-file fallback under `Data` needs the same prefix attempts
(loose assets live at `Data\meshes\...`, `Data\textures\...`).

### 4. Keep plugin files in scope

The project also recognizes `.esm`, `.esp`, and `.esl` files under the data directory as part of the Skyrim mod/plugin set. They are relevant for context, even if the actual `.nif` files may still be inside archives.

## Recommended implementation pattern

```csharp
// Registers every .bsa archive under the Skyrim Data folder into SkyrimData.
SkyrimData.Initialize(skyrimFolder);

if (SkyrimData.TryGet("Skyrim - Meshes.bsa", out var archiveInfo))
{
    foreach (var file in archiveInfo.Files)
    {
        if (string.Equals(Path.GetExtension(file), ".nif", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = archiveInfo.Open(file);
            // consume the entry
        }
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

## Location and model-record findings

The viewer can build a scene directly from a Skyrim location (an interior cell or an
exterior worldspace cell) by reading the plugin records with Mutagen.

### Loading a plugin

- Open a plugin read-only as an overlay:
  `SkyrimMod.CreateFromBinaryOverlay(ModPath path, SkyrimRelease release, BinaryReadParameters param = null)`
- The overlay returns `ISkyrimModDisposableGetter`, which derives from `ISkyrimModGetter`
  and `IDisposable` (dispose when done).
- `ModPath` has implicit conversions from `string` / `FilePath`.
- Build a lookup with the extension method
  `Mutagen.Bethesda.LinkCacheConstructionMixIn.ToImmutableLinkCache(this TModGetter mod)`
  (the base class is in the `Mutagen.Bethesda` namespace, not `.Plugins.Cache`).

### Enumerating cells (locations)

- `ISkyrimModGetter.Cells` is `ISkyrimListGroupGetter<ICellBlockGetter>`.
  It holds **interior** cells:
  `Cells.Records` (`ICellBlockGetter`) -> `SubBlocks` -> `ICellSubBlockGetter.Cells`.
- `ISkyrimModGetter.Worldspaces` is `ISkyrimGroupGetter<IWorldspaceGetter>`; enumerate
  `Worldspaces.RecordCache.Items`.
  Exterior cells live at:
  `IWorldspaceGetter.SubCells` -> `IWorldspaceBlockGetter.Items`
  -> `IWorldspaceSubBlockGetter.Items` (`ICellGetter`).
- `ICellGetter.Persistent` / `.Temporary` are `IReadOnlyList<IPlacedGetter>`.
- `ICellGetter.Grid` is `ICellGridGetter` with `Point` (`P2Int`, fields `.X` / `.Y`).
- Cell display name: `ICellGetter.Name` (`ITranslatedStringGetter.String`), falling back
  to `EditorID`. Worldspace name: `IWorldspaceGetter.Name.String`, falling back to `EditorID`.

Real `Skyrim.esm` counts: 590 interior cells, 16942 exterior cells.

### Resolving placed objects to models

- `ICellGetter` placed entries are `IPlacedGetter`; model-bearing ones are
  `IPlacedObjectGetter`.
- `IPlacedObjectGetter.Base` is `IFormLinkNullableGetter<IPlaceableObjectGetter>`.
  Resolve it with the `Mutagen.Bethesda.IFormLinkExt` extension
  `TryResolve<TMajor>(IFormLinkGetter<TMajor>, ILinkCache, out TMajor)`.
  The two-generic overload `TryResolve<TSource, TScopedMajor>` makes an unqualified call
  ambiguous, so specify the type argument explicitly.
- Placement: `IPlacedGetter.Placement` is `IPlacementGetter` with `Position` / `Rotation`
  (`P3Float`, `.X` / `.Y` / `.Z`, rotation in degrees). Scale is `IPlacedGetter.Scale`
  (`float?`, default 1).
- Model path: the resolved base record implements `IModeledGetter` (`Model : IModelGetter`),
  and `IModelGetter` derives from `ISimpleModelGetter` where
  `File` is `AssetLinkGetter<SkyrimModelAssetType>`. Read the raw path from
  `IAssetLinkGetter.GivenPath` (e.g. `Dungeons\Nordic\BgRooms\NorRmBgMid02.nif`).
- Many placed objects reference marker/effect models that are not present as real meshes;
  filter through the file system (`FileExists`) before loading.

### Implementation in this project

- `SkyrimLocations.cs` wraps the overlay + link cache and exposes a `SkyrimLocation` list
  (name + `ICellGetter` + optional `IWorldspaceGetter`).
- `SkyrimSceneBuilder.cs` walks a cell's placed objects, resolves each base record, loads
  its NIF mesh definitions and builds a `DrModel` with one transform bone group per object.
- `SkyrimFileSystem` now also falls back to loose files under `Data` (not just BSA entries)
  and exposes `FileExists` / `GetPluginPath`.
- `MainForm` has a source combo (`Locations` default, `Models`) above the filter box.

## NIF node transforms (placement fix)

NiflySharp `GetShapes()` returns vertices **in the shape's own local space**, ignoring the
transform of the shape itself and of every ancestor `NiNode`. For real Skyrim statics this
is frequently non-identity (observed on `Furniture\Common\CommonBedDouble01.nif`: mattress
pieces at translation `(-22.3, -0.24, 33.7)`, a bedpost piece `Object08` at `(32, -62.2,
42.2)` with a ~5 deg rotation). Ignoring them scrambles every multi-part model, which is
why furniture like beds appeared misplaced.

Fix in `NifModelLoader` (node-hierarchy approach):
- vertices are loaded **as-is** (shape-local space), no baking,
- `ParseTree(Stream)` walks the NIF tree from `NifFile.GetRootNodes()`, descending into
  `NiNode.Children` (enumerate by index via `Count` / `GetBlockRef(i)`, resolve with
  `GetBlock(int)`),
- each parsed node becomes a `NifTreeNode` carrying its **own local transform**
  (`NiAVObject.Translation` / `Rotation` / `Scale`) plus optional shape mesh data,
- `BuildNode` converts the tree to `DrModelBone`s whose `DefaultPose` is the node's local
  transform; the DrModel skeleton applies the full chain, so mesh world positions are
  correct without touching vertices.

NIF rotation matrices are stored column-major (nif.xml field order
`m11, m21, m31, m12, m22, m32, m13, m23, m33`); the field names follow the logical
(row, column), so the XNA row-vector matrix is the transpose of the natural reading.

Verified numerically: the hierarchy world composition (`local * parentWorld`) reproduces the
same assembled result as the old bake approach - `CommonBed01` frame `Z[0,74]`, mattress
pieces `Z[26,40]`, side rails at the board edges.

Locations load models through `SkyrimFileSystem.LoadModel` (same cache as the Models view), so
the NIF is parsed, materialized, and its GPU buffers are created exactly once per model path.
Each placed object in `SkyrimSceneBuilder` then gets a `NifModelLoader.CloneHierarchy` copy of
the cached bone tree whose mesh parts `Clone()` shares the source `VertexBuffer`/`IndexBuffer`;
the cache itself is never re-parented or mutated. The placement transform stays on the group
bone, the Z-up->Y-up root rotation on the scene root bone.
