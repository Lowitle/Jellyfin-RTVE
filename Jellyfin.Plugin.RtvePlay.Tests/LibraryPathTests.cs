using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the destination folder validation (the check behind the "Access to the path ... is
/// denied" failure seen when the folder is not mounted inside the container).
/// </summary>
public sealed class LibraryPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rtve-path-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Check_OnAnExistingWritableFolderSucceeds()
    {
        Directory.CreateDirectory(_root);

        var status = LibraryPath.Check(_root);

        Assert.True(status.IsValid);
        Assert.True(status.Exists);
        Assert.True(status.Writable);
        Assert.Null(status.Error);
        Assert.False(File.Exists(Path.Combine(_root, ".rtveplay-write-test")));
    }

    [Fact]
    public void Check_ReportsAMissingFolderWithoutCreatingIt()
    {
        var missing = Path.Combine(_root, "sub", "carpeta");

        var status = LibraryPath.Check(missing);

        Assert.True(status.IsValid);
        Assert.False(status.Exists);
        Assert.False(status.Writable);
        Assert.NotNull(status.Error);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void Check_RejectsEmptyAndRelativePaths()
    {
        Assert.False(LibraryPath.Check(null).IsValid);
        Assert.False(LibraryPath.Check("   ").IsValid);
        Assert.False(LibraryPath.Check("media/rtve-play").IsValid);
    }

    [Fact]
    public void EnsureWritableRoot_CreatesTheFolder()
    {
        var nested = Path.Combine(_root, "a", "b");

        LibraryPath.EnsureWritableRoot(nested);

        Assert.True(Directory.Exists(nested));
        Assert.False(File.Exists(Path.Combine(nested, ".rtveplay-write-test")));
    }

    [Fact]
    public void EnsureWritableRoot_RejectsRelativePaths()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => LibraryPath.EnsureWritableRoot("media/rtve-play"));

        Assert.Contains("carpeta de destino", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("absoluta", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureWritableRoot_ExplainsWhenThePathIsAFile()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "archivo.txt");
        File.WriteAllText(file, "x");

        var exception = Assert.Throws<InvalidOperationException>(() => LibraryPath.EnsureWritableRoot(file));

        Assert.Contains("carpeta de destino", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archivo.txt", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
