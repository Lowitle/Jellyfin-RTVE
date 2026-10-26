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
/// Folders are not exclusive: Jellyfin indexes a folder in every library that points at it, so a folder
/// that already belongs to another library is not a conflict. <see cref="Associate"/> lets the page add
/// one section to a library of the user without removing it from «RTVE Play».
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

                try
                {
                    foreach (var location in plan.Additions)
                    {
                        libraryManager.AddMediaPath(plan.LibraryName, new MediaPathInfo(location));
                    }

                    // The root contains the section folders: keeping both would index every series twice.
                    foreach (var location in plan.Removals)
                    {
                        libraryManager.RemoveMediaPath(plan.LibraryName, location);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                    or IOException or UnauthorizedAccessException)
                {
                    // Jellyfin refuses the request for a reason the planner cannot see (a path the server
                    // does not serve, a library that changed meanwhile...): say it instead of a 500.
                    logger.LogWarning(
                        "RTVE Play: Jellyfin rechazó las carpetas de {Library}: {Message}",
                        plan.LibraryName,
                        ex.Message);

                    return new LibraryProvisionStatus(
                        false,
                        "conflict",
                        plan.LibraryName,
                        fullPath,
                        $"Jellyfin no aceptó las carpetas de la biblioteca «{plan.LibraryName}»: {ex.Message}",
                        []);
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

                try
                {
                    await libraryManager
                        .AddVirtualFolder(plan.LibraryName, CollectionTypeOptions.tvshows, options, refreshLibrary: true)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                    or IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(
                        "RTVE Play: Jellyfin rechazó la biblioteca {Library}: {Message}",
                        plan.LibraryName,
                        ex.Message);

                    return new LibraryProvisionStatus(
                        false,
                        "conflict",
                        plan.LibraryName,
                        fullPath,
                        $"Jellyfin no aceptó crear la biblioteca «{plan.LibraryName}»: {ex.Message}",
                        []);
                }

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

        // Sharing a folder with another library is not an obstacle: Jellyfin indexes the same folder in
        // every library that points at it, so the user can keep one section in a library of their own
        // and still get the whole catalog in «RTVE Play». The page shows who owns what in «Carpetas y
        // bibliotecas» instead of refusing to create the library.
        //
        // The name, however, cannot be shared with a library of another type, even when the folders are
        // free: «AddMediaPath» looks the library up by name.
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

        // The library with the requested name is the one to update; when it does not exist, a TV library
        // created by a previous version is reused instead of creating a second one with the same
        // content: either it still points at the destination root, or it already indexes every section.
        // A library that only indexes some sections is a deliberate setup of the user, so the new
        // library is created next to it.
        var candidate = sameName
            ?? all.FirstOrDefault(library => library.IsTvShows
                && (library.Locations.Any(location => SamePath(location, root))
                    || locations.All(desired => library.Locations.Any(existing => SamePath(existing, desired)))));

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
    /// Describes the section folders of the destination and the libraries that index them, so the page
    /// can show who owns what and let the user associate a folder with another library.
    /// </summary>
    /// <remarks>
    /// The sections are always listed, even when the sync writes everything into the destination root:
    /// the table then says which folders the library would index with «Organizar» enabled.
    /// </remarks>
    /// <param name="path">Destination folder.</param>
    /// <param name="name">Name of the RTVE Play library; <see cref="DefaultLibraryName"/> when empty.</param>
    /// <param name="organizeSections">Whether the sync writes each program into the subfolder of its kind.</param>
    /// <param name="libraries">Libraries already configured in the server.</param>
    /// <returns>The section folders and the libraries of the server.</returns>
    public static LibrarySectionMatrix Describe(
        string path,
        string? name,
        bool organizeSections,
        IEnumerable<LibraryFolder>? libraries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = Path.TrimEndingDirectorySeparator(path.Trim());
        var libraryName = string.IsNullOrWhiteSpace(name) ? DefaultLibraryName : name.Trim();
        var all = libraries?.Where(library => library is not null).ToList() ?? [];

        var own = all.FirstOrDefault(library =>
            string.Equals(library.Name, libraryName, StringComparison.OrdinalIgnoreCase));

        // A library that still points at the root covers every section: that is the setup the library
        // button migrates to one folder per section.
        IReadOnlyList<string> ownLocations = own?.Locations ?? Array.Empty<string>();
        var coversEverything = ownLocations.Any(location => SamePath(location, root));

        var folders = ProgramSections.FolderNames
            .Select(folder => Path.Combine(root, folder))
            .Select(folder => new LibrarySectionFolder(
                Path.GetFileName(folder),
                folder,
                Directory.Exists(folder),
                coversEverything || ownLocations.Any(location => SamePath(location, folder)),
                all.Where(library => library.Locations.Any(location => SamePath(location, folder)))
                    .Select(library => library.Name)
                    .ToArray()))
            .ToArray();

        return new LibrarySectionMatrix(
            root,
            libraryName,
            organizeSections,
            own is not null,
            folders,
            all.Select(library => new LibrarySummary(
                library.Name,
                TypeLabel(library.CollectionType),
                library.IsTvShows,
                library.Locations))
                .ToArray(),
            folders.Where(folder => !folder.Covered).Select(folder => folder.Section).ToArray());
    }

    /// <summary>
    /// Decides whether a folder has to be added to a library, without touching the server.
    /// </summary>
    /// <param name="folder">Folder to associate.</param>
    /// <param name="libraryName">Library that has to index the folder.</param>
    /// <param name="libraries">Libraries already configured in the server.</param>
    /// <returns>The plan to apply.</returns>
    public static LibraryAssociationPlan PlanAssociation(
        string? folder,
        string? libraryName,
        IEnumerable<LibraryFolder>? libraries)
    {
        var normalized = Normalize(folder);

        if (normalized is null)
        {
            return new LibraryAssociationPlan(
                LibraryAssociationAction.Invalid,
                folder?.Trim() ?? string.Empty,
                null,
                "Indica la carpeta que quieres asociar a una biblioteca.");
        }

        var name = libraryName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return new LibraryAssociationPlan(
                LibraryAssociationAction.Invalid,
                normalized,
                null,
                "Elige la biblioteca a la que quieres asociar la carpeta.");
        }

        var all = libraries?.Where(library => library is not null).ToList() ?? [];
        var library = all.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            return new LibraryAssociationPlan(
                LibraryAssociationAction.Invalid,
                normalized,
                name,
                $"La biblioteca «{name}» no existe en este Jellyfin. Actualiza la página si la acabas de crear.");
        }

        if (library.Locations.Any(location => SamePath(location, normalized)))
        {
            return new LibraryAssociationPlan(
                LibraryAssociationAction.Existing,
                normalized,
                library.Name,
                $"La carpeta «{normalized}» ya estaba asociada a la biblioteca «{library.Name}»"
                + $" ({TypeLabel(library.CollectionType)}).");
        }

        return new LibraryAssociationPlan(
            LibraryAssociationAction.Added,
            normalized,
            library.Name,
            $"La carpeta «{normalized}» se asocia a la biblioteca «{library.Name}»"
            + $" ({TypeLabel(library.CollectionType)})."
            + (library.IsTvShows
                ? string.Empty
                : " La biblioteca no es de tipo «Programas de TV», así que Jellyfin puede no reconocer"
                    + " los archivos de las series."));
    }

    /// <summary>
    /// Adds a folder to a library of the server, so a section can live in a library of its own while
    /// the «RTVE Play» library keeps indexing every section.
    /// </summary>
    /// <remarks>
    /// Jellyfin indexes a folder in every library that points at it, so the same section can be in two
    /// libraries at once: nothing is removed from the library the folder belonged to.
    /// </remarks>
    /// <param name="libraryManager">Library manager of the server.</param>
    /// <param name="folder">Folder to associate.</param>
    /// <param name="libraryName">Library that has to index the folder.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="libraryMonitor">Library monitor used to tell Jellyfin about the new folder.</param>
    /// <returns>The result of the operation.</returns>
    public static LibraryAssociateStatus Associate(
        ILibraryManager libraryManager,
        string? folder,
        string? libraryName,
        ILogger logger,
        ILibraryMonitor? libraryMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(libraryManager);
        ArgumentNullException.ThrowIfNull(logger);

        var libraries = libraryManager.GetVirtualFolders() ?? [];
        var plan = PlanAssociation(folder, libraryName, ToFolders(libraries));

        switch (plan.Action)
        {
            case LibraryAssociationAction.Invalid:
                return new LibraryAssociateStatus(false, "invalid", plan.Folder, plan.LibraryName, plan.Message);

            case LibraryAssociationAction.Existing:
                return new LibraryAssociateStatus(true, "existing", plan.Folder, plan.LibraryName, plan.Message);

            default:
                // The folder is created before touching the library, so Jellyfin never stores a path
                // that does not exist yet.
                EnsureFolders([plan.Folder], logger);

                try
                {
                    libraryManager.AddMediaPath(plan.LibraryName!, new MediaPathInfo(plan.Folder));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                    or IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(
                        "RTVE Play: Jellyfin rechazó la carpeta {Folder} para {Library}: {Message}",
                        plan.Folder,
                        plan.LibraryName,
                        ex.Message);

                    return new LibraryAssociateStatus(
                        false,
                        "invalid",
                        plan.Folder,
                        plan.LibraryName,
                        $"Jellyfin no aceptó la carpeta «{plan.Folder}» en «{plan.LibraryName}»: {ex.Message}");
                }

                Notify(libraryMonitor, [plan.Folder], []);

                logger.LogInformation(
                    "RTVE Play: carpeta {Folder} añadida a la biblioteca {Library}",
                    plan.Folder,
                    plan.LibraryName);

                return new LibraryAssociateStatus(true, "added", plan.Folder, plan.LibraryName, plan.Message);
        }
    }

    /// <summary>
    /// Gets the name of a collection type in the language of the page.
    /// </summary>
    /// <param name="collectionType">Raw value of the collection type of a library.</param>
    /// <returns>The label shown to the user.</returns>
    private static string TypeLabel(string? collectionType) => collectionType switch
    {
        nameof(CollectionTypeOptions.movies) => "Películas",
        nameof(CollectionTypeOptions.tvshows) => "Programas de TV",
        nameof(CollectionTypeOptions.music) => "Música",
        nameof(CollectionTypeOptions.musicvideos) => "Vídeos musicales",
        nameof(CollectionTypeOptions.homevideos) => "Vídeos",
        nameof(CollectionTypeOptions.boxsets) => "Colecciones",
        nameof(CollectionTypeOptions.books) => "Libros",
        nameof(CollectionTypeOptions.mixed) => "Contenido mixto",
        "photos" => "Fotos",
        _ => "Sin tipo"
    };

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
    public static LibraryFolder[] ToFolders(IEnumerable<VirtualFolderInfo> libraries)
        => libraries
            .Select(library => new LibraryFolder(
                library.Name ?? string.Empty,
                IsTvLibrary(library),
                library.Locations?.Where(location => !string.IsNullOrWhiteSpace(location)).ToArray() ?? [],
                library.CollectionType?.ToString()))
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
    /// The name is already used by a library of another type.
    /// </summary>
    Conflict
}

/// <summary>
/// The data of a server library the planner needs, without depending on the Jellyfin assemblies.
/// </summary>
/// <param name="Name">Library name.</param>
/// <param name="IsTvShows">Whether the library is of type «Programas de TV».</param>
/// <param name="Locations">Folders of the library.</param>
/// <param name="CollectionType">Raw collection type of the library, <c>null</c> when it has none.</param>
public sealed record LibraryFolder(
    string Name,
    bool IsTvShows,
    IReadOnlyList<string> Locations,
    string? CollectionType = null);

/// <summary>
/// Section folder of the destination and the libraries that index it.
/// </summary>
/// <param name="Section">Folder name («peliculas», «series-de-tv»...).</param>
/// <param name="Path">Absolute path of the folder.</param>
/// <param name="Exists">Whether the folder exists on disk.</param>
/// <param name="Covered">Whether the RTVE Play library already indexes the folder.</param>
/// <param name="Libraries">Names of every library that indexes the folder.</param>
public sealed record LibrarySectionFolder(
    string Section,
    string Path,
    bool Exists,
    bool Covered,
    IReadOnlyList<string> Libraries);

/// <summary>
/// Library of the server, as the configuration page needs it to offer the association combo.
/// </summary>
/// <param name="Name">Library name.</param>
/// <param name="Type">Collection type in the language of the page («Programas de TV», «Películas»...).</param>
/// <param name="IsTvShows">Whether the library is of type «Programas de TV».</param>
/// <param name="Locations">Folders of the library.</param>
public sealed record LibrarySummary(
    string Name,
    string Type,
    bool IsTvShows,
    IReadOnlyList<string> Locations);

/// <summary>
/// Section folders of the destination and the libraries of the server, for the page table.
/// </summary>
/// <param name="Path">Destination folder.</param>
/// <param name="LibraryName">Name of the RTVE Play library.</param>
/// <param name="OrganizeSections">Whether the sync writes each program into the subfolder of its kind.</param>
/// <param name="LibraryExists">Whether the RTVE Play library already exists.</param>
/// <param name="Folders">Section folders of the destination.</param>
/// <param name="Libraries">Libraries of the server.</param>
/// <param name="Missing">Sections the RTVE Play library does not index yet.</param>
public sealed record LibrarySectionMatrix(
    string Path,
    string LibraryName,
    bool OrganizeSections,
    bool LibraryExists,
    IReadOnlyList<LibrarySectionFolder> Folders,
    IReadOnlyList<LibrarySummary> Libraries,
    IReadOnlyList<string> Missing);

/// <summary>
/// What has to be done to associate a folder with a library.
/// </summary>
public enum LibraryAssociationAction
{
    /// <summary>
    /// The request cannot be satisfied.
    /// </summary>
    Invalid,

    /// <summary>
    /// The folder has to be added to the library.
    /// </summary>
    Added,

    /// <summary>
    /// The library already indexes the folder.
    /// </summary>
    Existing
}

/// <summary>
/// Result of planning the association of a folder with a library, before touching the server.
/// </summary>
/// <param name="Action">Action to apply.</param>
/// <param name="Folder">Normalized folder.</param>
/// <param name="LibraryName">Library that has to index the folder.</param>
/// <param name="Message">Human readable explanation.</param>
public sealed record LibraryAssociationPlan(
    LibraryAssociationAction Action,
    string Folder,
    string? LibraryName,
    string Message);

/// <summary>
/// Result of the «associate folder» request.
/// </summary>
/// <param name="Success">Whether the folder ended up in the library.</param>
/// <param name="Outcome">Machine readable outcome: <c>added</c>, <c>existing</c> or <c>invalid</c>.</param>
/// <param name="Folder">Normalized folder.</param>
/// <param name="LibraryName">Library that was updated.</param>
/// <param name="Message">Message shown to the user.</param>
public sealed record LibraryAssociateStatus(
    bool Success,
    string Outcome,
    string Folder,
    string? LibraryName,
    string Message);

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
