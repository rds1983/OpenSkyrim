using System;
using System.IO;
using System.Linq;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;
using Xunit;

namespace OpenSkyrim.NifViewer.Tests;

public class NifModelLoaderTests
{
    [Fact]
    public void LoadMeshDefinitions_LoadsSimpleTriangleMesh()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Assets", "rug01.nif");
        Assert.True(File.Exists(fixturePath), $"Missing NIF fixture: {fixturePath}");

        var meshes = NifModelLoader.LoadMeshDefinitions(fixturePath);

        var mesh = Assert.Single(meshes);
        Assert.Equal("Rug01:0", mesh.Name);
        Assert.Equal(32, mesh.Vertices.Count);
        Assert.Equal(32, mesh.Normals.Count);
        Assert.Equal(32, mesh.Uvs.Count);
        Assert.Equal(48, mesh.Indices.Count);
        Assert.Equal(new[]
        {
            @"textures\creationclub\bgssse001\clutter\rugs\Rugs01.dds",
            @"textures\creationclub\bgssse001\clutter\rugs\Rugs01_n.dds"
        }, mesh.Textures);
    }

    [Fact]
    public void LoadMeshDefinitions_ThrowsForNonGamebryoStream()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(new byte[] { (byte)'N', (byte)'I', (byte)'F', 0 });
            writer.Write(0);
        }

        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => NifModelLoader.LoadMeshDefinitions(stream));
    }

    [Fact]
    public void LoadMeshDefinitions_LoadsRealSkyrimFishingMesh()
    {
        var bsaPath = Path.Combine(SkyrimData.DefaultSkyrimFolder, "Data", "ccBGSSSE001-Fish.bsa");
        if (!File.Exists(bsaPath))
        {
            return;
        }

        var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaPath));
        var nifFile = archive.Files.FirstOrDefault(f =>
            string.Equals(Path.GetExtension(f.Path), ".nif", StringComparison.OrdinalIgnoreCase)
            && f.Path.Contains("fishinggear_boxofworms01", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(nifFile);

        using var stream = new MemoryStream(nifFile!.GetBytes());
        var meshes = NifModelLoader.LoadMeshDefinitions(stream);

        Assert.NotEmpty(meshes);
        Assert.True(meshes.Sum(m => m.Vertices.Count) > 0);
        Assert.Contains(meshes, m => m.Name.Contains("FishingGear_BOXofWorms", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadMeshDefinitions_LoadsNiTriShapeDataMesh()
    {
        var bsaPath = Path.Combine(SkyrimData.DefaultSkyrimFolder, "Data", "Skyrim - Meshes0.bsa");
        if (!File.Exists(bsaPath))
        {
            return;
        }

        var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaPath));
        var nifFile = archive.Files.FirstOrDefault(f =>
            string.Equals(Path.GetExtension(f.Path), ".nif", StringComparison.OrdinalIgnoreCase)
            && f.Path.Contains("treepineforestash01", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(nifFile);

        using var stream = new MemoryStream(nifFile!.GetBytes());
        var meshes = NifModelLoader.LoadMeshDefinitions(stream);

        Assert.NotEmpty(meshes);
        Assert.Contains(meshes, m => m.Name.Contains("TreePineForestAsh01", StringComparison.OrdinalIgnoreCase));
        Assert.True(meshes.Sum(m => m.Vertices.Count) > 0);
    }
}