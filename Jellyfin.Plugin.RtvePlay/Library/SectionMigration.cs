namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Finds the folders a previous sync (or a previous classification) left a series in, so it can be
/// moved into its section folder once.
/// </summary>
internal static class SectionMigration
{
    /// <summary>
    /// Finds the folder the series has to be moved from.
    /// </summary>
    /// <param name="destinationPath">Configured destination folder.</param>
    /// <param name="targetRoot">Section folder the series belongs to.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <param name="organized">Whether the sections are enabled.</param>
    /// <returns>
    /// The folder to move, or <c>null</c> when there is nothing to move: the series is already in its
    /// section, it was never written or the sections are disabled.
    /// </returns>
    internal static string? FindLegacyFolder(
        string destinationPath,
        string targetRoot,
        string seriesTitle,
        bool organized)
    {
        var destination = Trim(destinationPath);
        var target = SeriesFolder(targetRoot, seriesTitle);

        if (Directory.Exists(target))
        {
            return null;
        }

        foreach (var root in CandidateRoots(destination, targetRoot, organized))
        {
            var legacy = SeriesFolder(root, seriesTitle);
            if (!SamePath(legacy, target) && Directory.Exists(legacy))
            {
                return legacy;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the copy a previous version wrote directly into the destination folder when the series
    /// already lives in its section.
    /// </summary>
    /// <param name="destinationPath">Configured destination folder.</param>
    /// <param name="targetRoot">Section folder the series belongs to.</param>
    /// <param name="seriesTitle">Series title.</param>
    /// <returns>
    /// The stale copy, or <c>null</c> when there is no duplicate. Jellyfin indexes the two folders as
    /// two different series, so the user has to be told about it.
    /// </returns>
    internal static string? FindDuplicateAtRoot(string destinationPath, string targetRoot, string seriesTitle)
    {
        var rootCopy = SeriesFolder(Trim(destinationPath), seriesTitle);
        var target = SeriesFolder(targetRoot, seriesTitle);

        return !SamePath(rootCopy, target) && Directory.Exists(rootCopy) && Directory.Exists(target)
            ? rootCopy
            : null;
    }

    /// <summary>
    /// Gets the folders a previous sync may have written the series into: the destination itself and,
    /// when the sections are enabled, every section folder except the one it belongs to.
    /// </summary>
    /// <param name="destination">Destination folder, without the trailing separator.</param>
    /// <param name="targetRoot">Section folder the series belongs to.</param>
    /// <param name="organized">Whether the sections are enabled.</param>
    /// <returns>The folders to look into, the most likely one first.</returns>
    private static IEnumerable<string> CandidateRoots(string destination, string targetRoot, bool organized)
    {
        if (!SamePath(destination, targetRoot))
        {
            yield return destination;
        }

        if (!organized)
        {
            yield break;
        }

        foreach (var folder in ProgramSections.FolderNames)
        {
            var root = Path.Combine(destination, folder);
            if (!SamePath(root, targetRoot))
            {
                yield return root;
            }
        }
    }

    private static string SeriesFolder(string root, string title)
        => Path.Combine(root, LibraryNaming.SeriesFolderName(title));

    private static string Trim(string path)
        => Path.TrimEndingDirectorySeparator(path.Trim());

    private static bool SamePath(string left, string right)
        => string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
