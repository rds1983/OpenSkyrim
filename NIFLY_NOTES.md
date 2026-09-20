# Nifly Notes

This project uses Nifly (NiflySharp) to load Gamebryo / NetImmerse `.nif` files and extract mesh and texture data.

## Key findings

- NiflySharp is the C# port of the C++ [nifly](https://github.com/ousnius/nifly) library, generated from the [nifxml](https://github.com/niftools/nifxml) spec.
- It parses NetImmerse, Gamebryo, and Creation Engine NIF formats, including Skyrim and Skyrim Special Edition.
- All `.nif` files in the project are loaded through `NifFile.Load` — there is no custom NIF parser.
- On Skyrim SE most meshes are `BSTriShape` / `BSDynamicTriShape`, whose vertex data is stored inside the shape block itself, not in a separate geometry data block.
- Texture paths are not derived from the mesh name; the shader of each shape provides the real texture set.

## Working pattern

### 1. Load a NIF

```csharp
var nifFile = new NifFile();
var result = nifFile.Load(stream, new NifFileLoadOptions());
```

- `Load` reads from the stream's current position, so seek the stream to `0` first when needed.
- A zero return value means success.
- Malformed input can throw an exception (for example `EndOfStreamException`) instead of returning an error code, so wrap loading in a `try/catch` and surface a consistent error.

### 2. Get the shapes

```csharp
foreach (var shape in nifFile.GetShapes())
```

`GetShapes()` returns `IEnumerable<INiShape>`. The shape name is available through `shape.Name.String`.

### 3. Read vertex data

There are two layouts, depending on the shape type:

- Older shapes such as `NiTriShape` reference a separate data block:
  - `shape.GeometryData` is a `NiGeometryData` with `Vertices`, `Normals`, `UVSets` (`List<TexCoord>`), and `Triangles`.
- Skyrim SE shapes such as `BSTriShape` / `BSDynamicTriShape` store data inline and expose it directly:
  - `VertexPositions`, `Normals`, `UVs` (`List<TexCoord>`), `Triangles`.
  - For these `shape.GeometryData` is `null`.

`INiShape` unifies the triangle data: `shape.Triangles` returns `List<Triangle>` (`V1` / `V2` / `V3` are `ushort`) for both layouts.

### 4. Read texture paths

Textures are not stored on the shape itself; they come from the shape's shader:

```csharp
var shader = nifFile.GetShader(shape);
if (shader?.HasTextureSet == true && !shader.TextureSetRef.IsEmpty())
{
    var set = nifFile.GetBlock(shader.TextureSetRef) as BSShaderTextureSet;
    foreach (var texture in set.Textures)
    {
        var path = texture.Content; // e.g. "textures\...\foo.dds"
    }
}
```

- The texture set is ordered: index `0` is the diffuse map, index `1` the normal map, etc.
- Entries are `NiString4`; the actual string is in `Content`.
- Paths use backslashes and are relative to the `Data` folder. Normalize `\` to `/` when looking them up in archives (the project's `ArchiveInfo` already does this).

## Important caveat

`NifFile.Load` can throw for malformed files; treat a thrown exception the same as a non-zero result. Geometry data lives either in `NiGeometryData` or directly on the `BSTriShape` — check both layouts instead of assuming a single shape API.

## Package version used

- `Nifly` version `1.1.0` (provides the `NiflySharp` namespace)