using System;
using System.IO;
using System.Linq;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog;
using Xunit;

namespace OpenSkyrim.NifViewer.Tests;

public class ArchiveInfoTests
{
    private static string? GetSampleArchivePath()
    {
        var dataFolder = Path.Combine(SkyrimData.DefaultSkyrimFolder, "Data");
        return Directory.EnumerateFiles(dataFolder, "*.bsa", SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    [Fact]
    public void Constructor_ThrowsFileNotFoundException_WhenArchiveDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "OpenSkyrim.Tests", Guid.NewGuid().ToString("N"), "missing.bsa");

        Assert.Throws<FileNotFoundException>(() => new ArchiveInfo(missingPath));
    }

    [Fact]
    public void Files_ReturnsSortedListOfArchiveFiles()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);

        Assert.True(archiveInfo.FileCount > 0);
        var files = archiveInfo.Files;
        Assert.Equal(files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), files);
    }

    [Fact]
    public void Contains_ReturnsTrueForExistingArchiveFile()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);
        var expectedPath = archiveInfo.Files[0];

        Assert.True(archiveInfo.Contains(expectedPath));
    }

    [Fact]
    public void Contains_ReturnsFalseForMissingArchiveFile()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);

        Assert.False(archiveInfo.Contains("missing/file.xyz"));
    }

    [Fact]
    public void Load_ReturnsSameBytesAsDirectMutagenAccess()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);
        var archivePath = archiveInfo.Files[0];

        var expectedBytes = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaPath))
            .Files
            .First(f => string.Equals(f.Path, archivePath, StringComparison.OrdinalIgnoreCase))
            .GetBytes();

        var actualBytes = archiveInfo.Load(archivePath);

        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public void Open_ReturnsReadableSeekableStream()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);
        var archivePath = archiveInfo.Files[0];

        using var stream = archiveInfo.Open(archivePath);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void Load_ThrowsFileNotFoundException_WhenFileIsMissing()
    {
        var bsaPath = GetSampleArchivePath();
        if (string.IsNullOrEmpty(bsaPath))
        {
            return;
        }

        var archiveInfo = new ArchiveInfo(bsaPath);

        Assert.Throws<FileNotFoundException>(() => archiveInfo.Load("missing/file.xyz"));
    }
}