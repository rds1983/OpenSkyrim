using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenSkyrim.NifViewer.Tests;

public class SkyrimDataTests
{
    private const string SampleBsaFile = "ccBGSSSE001-Fish.bsa";

    [Fact]
    public void ResolveSkyrimDataFolder_ReturnsDataDirectoryWhenGivenGameRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "Data"));

        var result = SkyrimData.ResolveSkyrimDataFolder(tempRoot);

        Assert.Equal(dataDirectory.FullName, result);
    }

    [Fact]
    public void Initialize_PopulatesArchivesFromDefaultInstall()
    {
        var sampleBsa = Path.Combine(SkyrimData.DefaultSkyrimFolder, "Data", SampleBsaFile);
        if (!File.Exists(sampleBsa))
        {
            return;
        }

        SkyrimData.Initialize(SkyrimData.DefaultSkyrimFolder);

        Assert.True(SkyrimData.Contains(SampleBsaFile));
        Assert.True(SkyrimData.TryGet(SampleBsaFile, out var archiveInfo));
        Assert.Equal(sampleBsa, archiveInfo!.ArchivePath);
        Assert.True(archiveInfo.FileCount > 0);
    }

    [Fact]
    public void Initialize_CanBeCalledMultipleTimes()
    {
        var sampleBsa = Path.Combine(SkyrimData.DefaultSkyrimFolder, "Data", SampleBsaFile);
        if (!File.Exists(sampleBsa))
        {
            return;
        }

        SkyrimData.Initialize(SkyrimData.DefaultSkyrimFolder);
        var count = SkyrimData.Archives.Count;

        SkyrimData.Initialize(SkyrimData.DefaultSkyrimFolder);

        Assert.Equal(count, SkyrimData.Archives.Count);
        Assert.True(SkyrimData.Contains(SampleBsaFile));
    }

    [Fact]
    public void Initialize_WithEmptyFolder_LeavesNoArchives()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            SkyrimData.Initialize(tempRoot);

            Assert.Empty(SkyrimData.Archives);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Initialize_SkipsUnreadableArchives()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "broken.bsa"), "not a bsa");

        try
        {
            SkyrimData.Initialize(tempRoot);

            Assert.False(SkyrimData.Contains("broken.bsa"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Get_ThrowsKeyNotFoundException_WhenArchiveIsNotRegistered()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            SkyrimData.Initialize(tempRoot);

            Assert.Throws<KeyNotFoundException>(() => SkyrimData.Get("missing.bsa"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TryGet_ReturnsFalseForMissingArchive()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            SkyrimData.Initialize(tempRoot);

            Assert.False(SkyrimData.TryGet("missing.bsa", out var archiveInfo));
            Assert.Null(archiveInfo);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}