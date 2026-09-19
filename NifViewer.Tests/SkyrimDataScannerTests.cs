using System;
using System.IO;
using Xunit;

namespace OpenSkyrim.NifViewer.Tests;

public class SkyrimDataScannerTests
{
    [Fact]
    public void ResolveSkyrimDataFolder_ReturnsDataDirectoryWhenGivenGameRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "Data"));

        var result = SkyrimDataScanner.ResolveSkyrimDataFolder(tempRoot);

        Assert.Equal(dataDirectory.FullName, result);
    }

    [Fact]
    public void EnumerateNifFiles_OnlyReturnsNifFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "Data"));

        var looseNif = Path.Combine(dataDirectory.FullName, "meshes", "test.nif");
        Directory.CreateDirectory(Path.GetDirectoryName(looseNif)!);
        File.WriteAllText(looseNif, "nif");

        var uppercaseNif = Path.Combine(dataDirectory.FullName, "textures", "mesh", "extra.NIF");
        Directory.CreateDirectory(Path.GetDirectoryName(uppercaseNif)!);
        File.WriteAllText(uppercaseNif, "nif");

        File.WriteAllText(Path.Combine(dataDirectory.FullName, "readme.txt"), "ignore");

        var files = SkyrimDataScanner.EnumerateNifFiles(tempRoot);

        Assert.Equal(2, files.Count);
        Assert.Contains(files, x => x.EndsWith("test.nif", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.EndsWith("extra.NIF", StringComparison.OrdinalIgnoreCase));
    }
}
