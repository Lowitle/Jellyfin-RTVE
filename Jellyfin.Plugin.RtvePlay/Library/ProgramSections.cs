namespace Jellyfin.Plugin.RtvePlay.Library;

/// <summary>
/// Subfolder of the destination folder where a program is written, so the series, the documentaries,
/// the magazines, the news... are kept apart without needing one library per kind.
/// </summary>
public enum ProgramSection
{
    /// <summary>Films and film containers («Cine»).</summary>
    Peliculas,

    /// <summary>Fiction series.</summary>
    Series,

    /// <summary>Documentaries.</summary>
    Documentales,

    /// <summary>Magazines («Magacín semanal», «Magacines diarios»...).</summary>
    Magacines,

    /// <summary>Other television programs: entertainment, interviews, specials, contests...</summary>
    Programas,

    /// <summary>News and current affairs.</summary>
    Noticias,

    /// <summary>Children's programs.</summary>
    Infantil,

    /// <summary>Japanese animation published inside the children's catalog.</summary>
    Anime,

    /// <summary>Video podcasts.</summary>
    Podcast,

    /// <summary>Historical archive («Series de Archivo»).</summary>
    Archivo,

    /// <summary>Everything RTVE publishes without a type this plugin can classify.</summary>
    Otros
}

/// <summary>
/// Maps the kind of program resolved by <see cref="ProgramClassifier"/> to a subfolder of the
/// destination folder. The names are lower case and without accents on purpose: the filesystem of a
/// NAS or of a Windows share must not decide whether two folders are the same one.
/// </summary>
public static class ProgramSections
{
    /// <summary>Folder of <see cref="ProgramSection.Peliculas"/>.</summary>
    public const string PeliculasFolder = "peliculas";

    /// <summary>Folder of <see cref="ProgramSection.Series"/>.</summary>
    public const string SeriesFolder = "series-de-tv";

    /// <summary>Folder of <see cref="ProgramSection.Documentales"/>.</summary>
    public const string DocumentalesFolder = "documentales";

    /// <summary>Folder of <see cref="ProgramSection.Magacines"/>.</summary>
    public const string MagacinesFolder = "magacines";

    /// <summary>Folder of <see cref="ProgramSection.Programas"/>.</summary>
    public const string ProgramasFolder = "programas-de-tv";

    /// <summary>Folder of <see cref="ProgramSection.Noticias"/>.</summary>
    public const string NoticiasFolder = "noticias";

    /// <summary>Folder of <see cref="ProgramSection.Infantil"/>.</summary>
    public const string InfantilFolder = "infantil";

    /// <summary>Folder of <see cref="ProgramSection.Anime"/>.</summary>
    public const string AnimeFolder = "anime";

    /// <summary>Folder of <see cref="ProgramSection.Podcast"/>.</summary>
    public const string PodcastFolder = "podcast";

    /// <summary>Folder of <see cref="ProgramSection.Archivo"/>.</summary>
    public const string ArchivoFolder = "archivo";

    /// <summary>Folder of <see cref="ProgramSection.Otros"/>.</summary>
    public const string OtrosFolder = "otros";

    /// <summary>
    /// Gets every folder name, in the order the sections are declared.
    /// </summary>
    public static IReadOnlyList<string> FolderNames { get; } =
    [
        PeliculasFolder,
        SeriesFolder,
        DocumentalesFolder,
        MagacinesFolder,
        ProgramasFolder,
        NoticiasFolder,
        InfantilFolder,
        AnimeFolder,
        PodcastFolder,
        ArchivoFolder,
        OtrosFolder
    ];

    /// <summary>
    /// Gets the folder of every section inside the destination folder.
    /// </summary>
    /// <param name="destinationPath">Destination folder of the library.</param>
    /// <returns>The folder of each section, in the order <see cref="FolderNames"/> declares them.</returns>
    public static IReadOnlyList<string> SectionPaths(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var root = Path.TrimEndingDirectorySeparator(destinationPath.Trim());

        return FolderNames.Select(folder => Path.Combine(root, folder)).ToArray();
    }

    /// <summary>
    /// Tells whether a folder is one of the section folders, which means the configured destination
    /// already points at a single section instead of at the root of the library.
    /// </summary>
    /// <param name="path">Folder to check.</param>
    /// <returns><c>true</c> when the last segment of the path is a section folder.</returns>
    public static bool IsSectionFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path.Trim()));

        return FolderNames.Any(folder => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Franchises of Japanese animation that RTVE publishes inside the children's catalog, which has
    /// no «Anime» type of its own. They are only checked once the program is already children's
    /// content, so a documentary called «Pokémon» would never land in <c>anime</c>.
    /// </summary>
    /// <remarks>
    /// Folded and with every separator turned into a space («Shin-chan» → «shin chan»), so the
    /// keywords are written with spaces only.
    /// </remarks>
    private static readonly string[] AnimeKeywords =
    [
        "dragon ball",
        "pokemon",
        "yu gi oh",
        "beyblade",
        "inazuma eleven",
        "digimon",
        "naruto",
        "one piece",
        "detective conan",
        "sailor moon",
        "shin chan",
        "doraemon"
    ];

    /// <summary>
    /// Gets the folder name of a section.
    /// </summary>
    /// <param name="section">Section.</param>
    /// <returns>The folder name, without separators.</returns>
    public static string FolderName(ProgramSection section)
        => section switch
        {
            ProgramSection.Peliculas => PeliculasFolder,
            ProgramSection.Series => SeriesFolder,
            ProgramSection.Documentales => DocumentalesFolder,
            ProgramSection.Magacines => MagacinesFolder,
            ProgramSection.Programas => ProgramasFolder,
            ProgramSection.Noticias => NoticiasFolder,
            ProgramSection.Infantil => InfantilFolder,
            ProgramSection.Anime => AnimeFolder,
            ProgramSection.Podcast => PodcastFolder,
            ProgramSection.Archivo => ArchivoFolder,
            _ => OtrosFolder
        };

    /// <summary>
    /// Resolves the section of a program.
    /// </summary>
    /// <param name="kind">Kind of program resolved by <see cref="ProgramClassifier"/>.</param>
    /// <param name="programType">Raw RTVE program type («Series de Archivo»).</param>
    /// <param name="title">Program title, used to spot the anime of the children's catalog.</param>
    /// <returns>The section the program belongs to.</returns>
    public static ProgramSection Resolve(string? kind, string? programType, string? title)
    {
        switch (kind)
        {
            case "Cine":
                return ProgramSection.Peliculas;

            case "Serie":
                // «Series de Archivo» is historical material (Telediarios, «Informe semanal»...), so it
                // is kept apart from the fiction series.
                return TextFolding.Fold(programType).Contains("archivo", StringComparison.Ordinal)
                    ? ProgramSection.Archivo
                    : ProgramSection.Series;

            case "Documental":
                return ProgramSection.Documentales;

            case "Magacín":
                return ProgramSection.Magacines;

            case "Informativo":
                return ProgramSection.Noticias;

            case "Infantil":
                return IsAnime(title) ? ProgramSection.Anime : ProgramSection.Infantil;

            case "Podcast":
                return ProgramSection.Podcast;

            case "Concurso":
            case "Entrevistas":
            case "Especial":
            case "Entretenimiento":
            case "Música":
            case "Cocina":
                return ProgramSection.Programas;

            default:
                return ProgramSection.Otros;
        }
    }

    /// <summary>
    /// Gets the folder where the series of a program has to be written.
    /// </summary>
    /// <param name="destinationPath">Configured destination folder.</param>
    /// <param name="organized">Whether the sections are enabled.</param>
    /// <param name="section">Section of the program.</param>
    /// <returns>The folder the series belongs to.</returns>
    /// <remarks>
    /// When the destination already points at the section folder it is not appended again, so a user
    /// who prefers one library per kind can set <c>/media/rtve-play/documentales</c> as destination
    /// without ending up with <c>documentales/documentales</c>.
    /// </remarks>
    public static string RootFor(string destinationPath, bool organized, ProgramSection section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var root = Path.TrimEndingDirectorySeparator(destinationPath.Trim());
        if (!organized)
        {
            return root;
        }

        var folder = FolderName(section);

        return string.Equals(Path.GetFileName(root), folder, StringComparison.OrdinalIgnoreCase)
            ? root
            : Path.Combine(root, folder);
    }

    /// <summary>
    /// Tells whether a title belongs to a franchise of Japanese animation.
    /// </summary>
    /// <param name="title">Program title.</param>
    /// <returns><c>true</c> when the title matches a known franchise.</returns>
    private static bool IsAnime(string? title)
    {
        var name = TextFolding.Fold(title).Replace('-', ' ').Replace('_', ' ');

        return name.Length > 0
            && AnimeKeywords.Any(keyword => name.Contains(keyword, StringComparison.Ordinal));
    }
}
