using System.Security;
using System.Text.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Creates (or reuses) a «Programas de TV» library that points at the destination folder, so a fresh
/// installation does not need a trip through the «Add media library» wizard.
/// </summary>
/// <remarks>
/// RTVE videos flagged with <c>hasDRM</c> still expose a plain progressive MP4 and an unencrypted
/// HLS playlist (the flag comes from the RTVE CMS, not from the served asset), so the library only
/// has to index the <c>.strm</c> files this plugin writes.
/// </remarks>
public static class LibraryProvisioner
{
    /// <summary>
    /// Library name used when the page does not send one.
    /// </summary>
    public const string DefaultLibraryName = "RTVE Play";

    private static readonly JsonSerializerOptions CloneOptions = new();

    /// <summary>
    /// Creates the library (or adds the folder to an existing one) and asks Jellyfin to scan it.
    /// </summary>
    /// <param name="libraryManager">Library manager of the server.</param>
    /// <param name="path">Destination folder.</param>
    /// <param name="name">Library name; <see cref="DefaultLibraryName"/> when empty.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<LibraryProvisionStatus> ProvisionAsync(
        ILibraryManager libraryManager,
        string? path,
        string? name,
        ILogger logger,
        CancellationToken cancellationToken)
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
                "Escribe la carpeta de destino antes de crear la biblioteca.");
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
            return new LibraryProvisionStatus(false, "invalid", null, value, ex.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var libraries = libraryManager.GetVirtualFolders() ?? [];
        var plan = Plan(libraryName, fullPath, ToFolders(libraries));

        switch (plan.Action)
        {
            case LibraryProvisionAction.Invalid:
                return new LibraryProvisionStatus(false, "invalid", null, fullPath, plan.Message);

            case LibraryProvisionAction.Conflict:
                return new LibraryProvisionStatus(false, "conflict", plan.ExistingLibrary, fullPath, plan.Message);

            case LibraryProvisionAction.AlreadyPresent:
                return new LibraryProvisionStatus(
                    true,
                    "existing",
                    plan.ExistingLibrary,
                    fullPath,
                    $"La carpeta ya está asociada a la biblioteca «{plan.ExistingLibrary}»; no hay nada que hacer.");

            case LibraryProvisionAction.AddPath:
                libraryManager.AddMediaPath(plan.LibraryName, new MediaPathInfo(fullPath));
                return new LibraryProvisionStatus(
                    true,
                    "updated",
                    plan.LibraryName,
                    fullPath,
                    $"Carpeta asociada a la biblioteca «{plan.LibraryName}», que ya existía.");

            default:
                var options = BuildOptions(fullPath, libraries);
                await libraryManager
                    .AddVirtualFolder(plan.LibraryName, CollectionTypeOptions.tvshows, options, refreshLibrary: true)
                    .ConfigureAwait(false);

                return new LibraryProvisionStatus(
                    true,
                    "created",
                    plan.LibraryName,
                    fullPath,
                    $"Biblioteca «{plan.LibraryName}» creada. Jellyfin la está escaneando ahora mismo.");
        }
    }

    /// <summary>
    /// Decides what to do with the requested library without touching the server.
    /// </summary>
    /// <param name="libraryName">Name requested for the library.</param>
    /// <param name="path">Destination folder.</param>
    /// <param name="libraries">Libraries already configured in the server.</param>
    /// <returns>The plan to apply.</returns>
    public static LibraryProvisionPlan Plan(string libraryName, string path, IEnumerable<LibraryFolder>? libraries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var all = libraries?.Where(library => library is not null).ToList() ?? [];

        foreach (var library in all)
        {
            if (!library.Locations.Any(location => SamePath(location, path)))
            {
                continue;
            }

            return library.IsTvShows
                ? new LibraryProvisionPlan(
                    LibraryProvisionAction.AlreadyPresent,
                    library.Name,
                    library.Name,
                    $"La carpeta ya está asociada a la biblioteca «{library.Name}».")
                : new LibraryProvisionPlan(
                    LibraryProvisionAction.Conflict,
                    library.Name,
                    library.Name,
                    $"La carpeta ya está asociada a la biblioteca «{library.Name}», que no es de tipo «Programas de TV». "
                    + "Usa otra carpeta o quita esa ruta de la biblioteca actual.");
        }

        var sameName = all.FirstOrDefault(library =>
            string.Equals(library.Name, libraryName, StringComparison.OrdinalIgnoreCase));

        if (sameName is not null)
        {
            return sameName.IsTvShows
                ? new LibraryProvisionPlan(
                    LibraryProvisionAction.AddPath,
                    sameName.Name,
                    sameName.Name,
                    $"Se añadirá la carpeta a la biblioteca «{sameName.Name}», que ya existe.")
                : new LibraryProvisionPlan(
                    LibraryProvisionAction.Conflict,
                    sameName.Name,
                    sameName.Name,
                    $"Ya existe una biblioteca llamada «{sameName.Name}» que no es de tipo «Programas de TV»; "
                    + "cambia el nombre de la biblioteca o elige otra carpeta.");
        }

        return new LibraryProvisionPlan(
            LibraryProvisionAction.Create,
            libraryName,
            null,
            $"Se creará la biblioteca «{libraryName}» de tipo «Programas de TV».");
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
    private static LibraryOptions BuildOptions(string path, IReadOnlyList<VirtualFolderInfo> libraries)
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

        // The folder of this library is set here, never copied from the template.
        options.PathInfos = [new MediaPathInfo(path)];
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
    /// The folder has to be added to an existing library with the requested name.
    /// </summary>
    AddPath,

    /// <summary>
    /// A library already contains the folder.
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
/// <param name="Message">Human readable explanation.</param>
public sealed record LibraryProvisionPlan(
    LibraryProvisionAction Action,
    string LibraryName,
    string? ExistingLibrary,
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
public sealed record LibraryProvisionStatus(
    bool Success,
    string Outcome,
    string? LibraryName,
    string Path,
    string Message);
