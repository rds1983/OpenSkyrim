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
    public void ParseMeshDefinitions_LoadsSimpleTriangleMesh()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"), "sample.nif");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        using (var stream = File.Create(tempPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new byte[] { (byte)'N', (byte)'I', (byte)'F', 0 });
            writer.Write(1);

            writer.Write("SimpleMesh");
            writer.Write(3);
            writer.Write(0f); writer.Write(0f); writer.Write(0f);
            writer.Write(1f); writer.Write(0f); writer.Write(0f);
            writer.Write(0f); writer.Write(1f); writer.Write(0f);

            writer.Write(3);
            writer.Write(0f); writer.Write(0f); writer.Write(1f);
            writer.Write(0f); writer.Write(0f); writer.Write(1f);
            writer.Write(0f); writer.Write(1f); writer.Write(0f);

            writer.Write(3);
            writer.Write(0f); writer.Write(0f);
            writer.Write(1f); writer.Write(0f);
            writer.Write(0f); writer.Write(1f);

            writer.Write(3);
            writer.Write(0); writer.Write(1); writer.Write(2);
        }

        var meshes = NifModelLoader.LoadMeshDefinitions(tempPath);

        Assert.Single(meshes);
        Assert.Equal("SimpleMesh", meshes[0].Name);
        Assert.Equal(3, meshes[0].Vertices.Count);
        Assert.Equal(3, meshes[0].Indices.Count);
    }

    [Fact]
    public void ParseMeshDefinitions_LoadsRealSkyrimFishingMesh()
    {
        var bsaPath = Path.Combine(SkyrimDataScanner.DefaultSkyrimFolder, "Data", "ccBGSSSE001-Fish.bsa");
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
    }

    [Fact]
    public void ParseDdsFromArchive_LoadsRealSkyrimTextureHeader()
    {
        var dataFolder = Path.Combine(SkyrimDataScanner.DefaultSkyrimFolder, "Data");
        var bsaPath = Directory.EnumerateFiles(dataFolder, "Skyrim - Textures*.bsa", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaPath));
        var ddsFile = archive.Files.FirstOrDefault(f =>
            string.Equals(Path.GetExtension(f.Path), ".dds", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ddsFile);

        using var stream = new MemoryStream(ddsFile!.GetBytes());
        var info = DrModelViewWidget.ParseDdsHeader(stream);

        Assert.True(info.Width > 0);
        Assert.True(info.Height > 0);
        Assert.NotEqual(0u, info.PixelFormat);
    }
}
