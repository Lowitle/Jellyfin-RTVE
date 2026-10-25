using System.Security;
using System.Text.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Creates (or reuses) a «Programas de TV» library that points at the folders of the destination, so
/// a fresh installation does not need a trip through the «Add media library» wizard.
/// </summary>
/// <remarks>
/// <para>
/// With the sections enabled the library is associated with each section folder (<c>peliculas</c>,
/// <c>series-de-tv</c>...) instead of with the destination root. Jellyfin indexes the same content
/// either way, but each folder of the library then matches a section of the sync, so one kind of
/// program can be moved to another library or excluded without touching the rest.
/// </para>
/// <para>
/// A library created by a previous version that points at the root is migrated in place: the section
/// folders are added and the root is removed, because the root already contains them and Jellyfin
/// would index every series twice.
/// </para>
/// <para>
/// RTVE videos flagged with <c>hasDRM</c> still expose a plain progressive MP4 and an unencrypted
/// HLS playlist (the flag comes from the RTVE CMS, not from the served asset), so the library only
/// has to index the <c>.strm</c> files this plugin writes.
/// </para>
/// </remarks>
public static class LibraryProvisioner
{
    /// <summary>
    /// Library name used when the page does not send one.
    /// </summary>
    public const string DefaultLibraryName = "RTVE Play";

    private static readonly JsonSerializerOptions CloneOptions = new();

    /// <summary>
    /// Creates the library (or updates the folders of an existing one) and asks Jellyfin to scan it.
    /// </summary>
    /// <param name="libraryManager">Library manager of the server.</param>
    /// <param name="path">Destination folder.</param>
    /// <param name="name">Library name; <see cref="DefaultLibraryName"/> when empty.</param>
    /// <param name="organizeSections">
    /// Whether the sync writes each program into the subfolder of its kind. When enabled, the folder
    /// of the library is the section folder, not the destination root.
    /// </param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="libraryMonitor">Library monitor used to tell Jellyfin about the new folders.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<LibraryProvisionStatus> ProvisionAsync(
        ILibraryManager libraryManager,
        string? path,
        string? name,
        bool organizeSections,
        ILogger logger,
        CancellationToken cancellationToken,
        ILibraryMonitor? libraryMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(libraryManager);
        ArgumentNullException.ThrowIfNull(logger);

        var libraryName = string.IsNullOrWhiteSpace(name) ? DefaultLibraryName : name.Trim();
        var value = path?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return new LibraryProvisionStatus(
                false,
                "invalid",
                null,
                value,
                "Escribe la carpeta de destino antes de crear la biblioteca.",
                []);
        }

        string fullPath;
        try
        {
            LibraryPath.EnsureWritableRoot(value);
            fullPath = Normalize(value)!;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException
            or PathTooLongException or SecurityException)
        {
            logger.LogWarning("RTVE Play: no se puede usar {Path} como biblioteca: {Message}", value, ex.Message);
            return new LibraryProvisionStatus(false, "invalid", null, value, ex.Message, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var libraries = libraryManager.GetVirtualFolders() ?? [];
        var plan = Plan(libraryName, fullPath, ToFolders(libraries), organizeSections);

        switch (plan.Action)
        {
            case LibraryProvisionAction.Invalid:
                return new LibraryProvisionStatus(false, "invalid", null, fullPath, plan.Message, []);

            case LibraryProvisionAction.Conflict:
                return new LibraryProvisionStatus(false, "conflict", plan.ExistingLibrary, fullPath, plan.Message, []);

            case LibraryProvisionAction.AlreadyPresent:
                return new LibraryProvisionStatus(true, "existing", plan.ExistingLibrary, fullPath, plan.Message, []);

            case LibraryProvisionAction.AddPath:
                // The folders are created before touching the library, so Jellyfin never stores a path
                // that does not exist yet.
                EnsureFolders(plan.Additions, logger);

                foreach (var location in plan.Additions)
                {
                    libraryManager.AddMediaPath(plan.LibraryName, new MediaPathInfo(location));
                }

                // The root contains the section folders: keeping both would index every series twice.
                foreach (var location in plan.Removals)
                {
                    libraryManager.RemoveMediaPath(plan.LibraryName, location);
                }

                Notify(libraryMonitor, plan.Additions, plan.Removals);

                return new LibraryProvisionStatus(
                    true,
                    "updated",
                    plan.LibraryName,
                    fullPath,
                    plan.Message + " Jellyfin está revisando las carpetas nuevas.",
                    plan.Additions);

            default:
                EnsureFolders(plan.Additions, logger);

                var options = BuildOptions(plan.Additions, libraries);
                await libraryManager
                    .AddVirtualFolder(plan.LibraryName, CollectionTypeOptions.tvshows, options, refreshLibrary: true)
                    .ConfigureAwait(false);

                return new LibraryProvisionStatus(
                    true,
                    "created",
                    plan.LibraryName,
                    fullPath,
                    plan.Message + " Jellyfin la está escaneando ahora mismo.",
                    plan.Additions);
        }
    }

    /// <summary>
    /// Decides what to do with the requested library without touching the server.
    /// </summary>
    /// <param name="libraryName">Name requested for the library.</param>
    /// <param name="path">Destination folder.</param>
    /// <param name="libraries">Libraries already configured in the server.</param>
    /// <param name="organizeSections">
    /// Whether the library has to point at the section folders instead of at the destination root.
    /// </param>
    /// <returns>The plan to apply.</returns>
    public static LibraryProvisionPlan Plan(
        string libraryName,
        string path,
        IEnumerable<LibraryFolder>? libraries,
        bool organizeSections = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = Path.TrimEndingDirectorySeparator(path.Trim());
        var locations = Locations(root, organizeSections);
        var all = libraries?.Where(library => library is not null).ToList() ?? [];

        // A library of another type that already covers one of the folders would index the same
        // series twice, so nothing is touched until the user removes that path.
        var foreign = all.FirstOrDefault(library =>
            !library.IsTvShows && library.Locations.Any(location => Covered(location, root, locations)));

        if (foreign is not null)
        {
            var offender = foreign.Locations.FirstOrDefault(location => Covered(location, root, locations)) ?? root;

            return new LibraryProvisionPlan(
                LibraryProvisionAction.Conflict,
                libraryName,
                foreign.Name,
                [],
                [],
                $"La carpeta ya está asociada a la biblioteca «{foreign.Name}» ({offender}), que no es de tipo «Programas de TV». "
                + "Usa otra carpeta o quita esa ruta de la biblioteca actual.");
        }

        // The name cannot be shared with a library of another type, even when the folders are free.
        var sameName = all.FirstOrDefault(library =>
            string.Equals(library.Name, libraryName, StringComparison.OrdinalIgnoreCase));

        if (sameName is not null && !sameName.IsTvShows)
        {
            return new LibraryProvisionPlan(
                LibraryProvisionAction.Conflict,
                sameName.Name,
                sameName.Name,
                [],
                [],
                $"Ya existe una biblioteca llamada «{sameName.Name}» que no es de tipo «Programas de TV»; "
                + "cambia el nombre de la biblioteca o elige otra carpeta.");
        }

        // The library with the requested name is the one to update; when it does not exist, a TV
        // library created by a previous version already pointing at the root is reused instead of
        // creating a second one with the same content.
        var candidate = sameName
            ?? all.FirstOrDefault(library =>
                library.IsTvShows && library.Locations.Any(location => Covered(location, root, locations)));

        if (candidate is null)
        {
            return new LibraryProvisionPlan(
                LibraryProvisionAction.Create,
                libraryName,
                null,
                locations,
                [],
                locations.Count > 1
                    ? $"Se creará la biblioteca «{libraryName}» de tipo «Programas de TV» con {locations.Count} carpetas, una por tipo de programa."
                    : $"Se creará la biblioteca «{libraryName}» de tipo «Programas de TV».");
        }

        var additions = locations
            .Where(location => !candidate.Locations.Any(existing => SamePath(existing, location)))
            .ToArray();

        // Only the destination root is superseded: it is the folder that contains every section, so
        // keeping it would make Jellyfin index the same series twice. Other folders of the library
        // are left alone.
        var removals = candidate.Locations
            .Where(location => SamePath(location, root) && !locations.Any(desired => SamePath(desired, location)))
            .ToArray();

        if (additions.Length == 0 && removals.Length == 0)
        {
            return new LibraryProvisionPlan(
                LibraryProvisionAction.AlreadyPresent,
                candidate.Name,
                candidate.Name,
                [],
                [],
                locations.Count > 1
                    ? $"La biblioteca «{candidate.Name}» ya tiene asociadas las {locations.Count} carpetas; no hay nada que hacer."
                    : $"La carpeta ya está asociada a la biblioteca «{candidate.Name}»; no hay nada que hacer.");
        }

        var message = (additions.Length, removals.Length) switch
        {
            (0, _) => $"La biblioteca «{candidate.Name}» ya tiene asociadas las {locations.Count} carpetas; "
                + "se quitará de ella la carpeta raíz, que las incluye.",
            (_, 0) => $"Se añadirán {additions.Length} carpetas a la biblioteca «{candidate.Name}», que ya existe.",
            _ => $"Se añadirán {additions.Length} carpetas a la biblioteca «{candidate.Name}» y se quitará de ella "
                + "la carpeta raíz, que las incluye."
        };

        return new LibraryProvisionPlan(
            LibraryProvisionAction.AddPath,
            candidate.Name,
            candidate.Name,
            additions,
            removals,
            message);
    }

    /// <summary>
    /// Gets the folders the library has to index: one per section, or the destination root when the
    /// sections are disabled or when the destination already points at a single section.
    /// </summary>
    /// <param name="root">Destination folder, without the trailing separator.</param>
    /// <param name="organizeSections">Whether the sync writes each program into the subfolder of its kind.</param>
    /// <returns>The folders of the library.</returns>
    private static IReadOnlyList<string> Locations(string root, bool organizeSections)
        => organizeSections && !ProgramSections.IsSectionFolder(root)
            ? ProgramSections.SectionPaths(root)
            : [root];

    /// <summary>
    /// Tells whether a library folder already indexes the content of the requested folders, either
    /// because it is one of them or because it is the destination root that contains all of them.
    /// </summary>
    /// <param name="location">Folder of an existing library.</param>
    /// <param name="root">Destination folder.</param>
    /// <param name="locations">Folders the library has to index.</param>
    /// <returns><c>true</c> when the folder overlaps with the requested ones.</returns>
    private static bool Covered(string? location, string root, IReadOnlyList<string> locations)
        => SamePath(location, root) || locations.Any(desired => SamePath(desired, location));

    /// <summary>
    /// Creates the folders that are about to be added to the library, so Jellyfin never stores a
    /// path that does not exist yet (the sync only creates them when it writes the first program).
    /// </summary>
    /// <param name="locations">Folders of the library.</param>
    /// <param name="logger">Logger.</param>
    private static void EnsureFolders(IReadOnlyList<string> locations, ILogger logger)
    {
        foreach (var location in locations)
        {
            try
            {
                Directory.CreateDirectory(location);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
            {
                // The path is added anyway: the sync reports what it cannot write, and a missing
                // folder shows up in Jellyfin as an empty section of the library.
                logger.LogWarning("RTVE Play: no se pudo crear {Path} al añadirlo a la biblioteca: {Message}", location, ex.Message);
            }
        }
    }

    /// <summary>
    /// Tells the library monitor about the folders that changed, so Jellyfin refreshes them without
    /// waiting for the next scheduled scan.
    /// </summary>
    /// <param name="libraryMonitor">Library monitor; ignored when the server did not provide one.</param>
    /// <param name="added">Folders added to the library.</param>
    /// <param name="removed">Folders removed from the library.</param>
    private static void Notify(ILibraryMonitor? libraryMonitor, IEnumerable<string> added, IEnumerable<string> removed)
    {
        if (libraryMonitor is null)
        {
            return;
        }

        foreach (var location in added.Concat(removed))
        {
            libraryMonitor.ReportFileSystemChanged(location);
        }
    }

    private static bool IsTvLibrary(VirtualFolderInfo library)
        => library.CollectionType == CollectionTypeOptions.tvshows;

    /// <summary>
    /// Reduces the libraries of the server to the data the planner needs, so it can be tested
    /// without the Jellyfin assemblies.
    /// </summary>
    /// <param name="libraries">Libraries of the server.</param>
    /// <returns>The same libraries as plain records.</returns>
    private static LibraryFolder[] ToFolders(IEnumerable<VirtualFolderInfo> libraries)
        => libraries
            .Select(library => new LibraryFolder(
                library.Name ?? string.Empty,
                IsTvLibrary(library),
                library.Locations?.Where(location => !string.IsNullOrWhiteSpace(location)).ToArray() ?? []))
            .ToArray();

    /// <summary>
    /// Builds the options of the new library, reusing the ones of an existing TV library (they carry
    /// the metadata and image providers the user already tuned) and falling back to sane defaults.
    /// </summary>
    /// <param name="locations">Folders of the new library.</param>
    /// <param name="libraries">Libraries of the server.</param>
    /// <returns>The options of the library.</returns>
    private static LibraryOptions BuildOptions(IReadOnlyList<string> locations, IReadOnlyList<VirtualFolderInfo> libraries)
    {
        var template = libraries
            .FirstOrDefault(library => IsTvLibrary(library) && library.LibraryOptions is not null)
            ?.LibraryOptions;

        var options = template is null ? new LibraryOptions() : Clone(template);

        options.Enabled = true;
        options.EnablePhotos = false;
        options.EnableRealtimeMonitor = true;
        options.SaveLocalMetadata = true;
        options.SaveSubtitlesWithMedia = true;
        options.EnableAutomaticSeriesGrouping = true;
        options.EnableEmbeddedTitles = true;
        options.EnableEmbeddedEpisodeInfos = true;
        options.MetadataSavers ??= ["Nfo"];
        options.LocalMetadataReaderOrder ??= ["Nfo"];
        if (string.IsNullOrWhiteSpace(options.SeasonZeroDisplayName))
        {
            options.SeasonZeroDisplayName = "Especiales";
        }

        // The folders of this library are set here, never copied from the template.
        options.PathInfos = locations.Select(location => new MediaPathInfo(location)).ToArray();
        return options;
    }

    private static LibraryOptions Clone(LibraryOptions options)
        => JsonSerializer.Deserialize<LibraryOptions>(
            JsonSerializer.Serialize(options, CloneOptions),
            CloneOptions) ?? new LibraryOptions();

    private static bool SamePath(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a is null || b is null)
        {
            return false;
        }

        return string.Equals(
            a,
            b,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return null;
        }
    }
}

/// <summary>
/// What has to be done to get the destination folder into a library.
/// </summary>
public enum LibraryProvisionAction
{
    /// <summary>
    /// The request cannot be satisfied.
    /// </summary>
    Invalid,

    /// <summary>
    /// A new «Programas de TV» library has to be created.
    /// </summary>
    Create,

    /// <summary>
    /// The folders have to be added to (and the destination root removed from) an existing library.
    /// </summary>
    AddPath,

    /// <summary>
    /// A library already contains the folders of the destination.
    /// </summary>
    AlreadyPresent,

    /// <summary>
    /// The folder or the name is already used by a library of another type.
    /// </summary>
    Conflict
}

/// <summary>
/// The data of a server library the planner needs, without depending on the Jellyfin assemblies.
/// </summary>
/// <param name="Name">Library name.</param>
/// <param name="IsTvShows">Whether the library is of type «Programas de TV».</param>
/// <param name="Locations">Folders of the library.</param>
public sealed record LibraryFolder(string Name, bool IsTvShows, IReadOnlyList<string> Locations);

/// <summary>
/// Result of planning the library, before touching the server.
/// </summary>
/// <param name="Action">Action to apply.</param>
/// <param name="LibraryName">Library to create or update.</param>
/// <param name="ExistingLibrary">Library that already exists, when applicable.</param>
/// <param name="Additions">Folders to add to the library.</param>
/// <param name="Removals">Folders to remove from the library.</param>
/// <param name="Message">Human readable explanation.</param>
public sealed record LibraryProvisionPlan(
    LibraryProvisionAction Action,
    string LibraryName,
    string? ExistingLibrary,
    IReadOnlyList<string> Additions,
    IReadOnlyList<string> Removals,
    string Message);

/// <summary>
/// Result of the «create library» request.
/// </summary>
/// <param name="Success">Whether the folder ended up in a library.</param>
/// <param name="Outcome">
/// Machine readable outcome: <c>created</c>, <c>updated</c>, <c>existing</c>, <c>conflict</c> or <c>invalid</c>.
/// </param>
/// <param name="LibraryName">Library that was created or updated.</param>
/// <param name="Path">Normalized destination folder.</param>
/// <param name="Message">Message shown to the user.</param>
/// <param name="LibraryPaths">Folders the library indexes after the request.</param>
public sealed record LibraryProvisionStatus(
    bool Success,
    string Outcome,
    string? LibraryName,
    string Path,
    string Message,
    IReadOnlyList<string> LibraryPaths);
