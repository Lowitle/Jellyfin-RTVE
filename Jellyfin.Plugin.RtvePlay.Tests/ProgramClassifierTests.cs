using Jellyfin.Plugin.RtvePlay.Library;
using Xunit;

namespace Jellyfin.Plugin.RtvePlay.Tests;

/// <summary>
/// Tests of the mapping between the RTVE program type and the Jellyfin genres.
/// </summary>
public class ProgramClassifierTests
{
    [Theory]
    [InlineData("132536", "Serie Documental", "Documental")]
    [InlineData("136523", "Series Ficción Originales", "Serie")]
    [InlineData("145350", "Infantiles", "Infantil")]
    [InlineData("137310", "Contenedor Películas", "Cine")]
    [InlineData("136612", "Magacín semanal", "Magacín")]
    [InlineData("1001560", "Videopodcast", "Podcast")]
    [InlineData("136521", "Reportajes Factual", "Documental")]
    [InlineData("132534", "Informativos Noticias", "Informativo")]
    [InlineData("136527", "Recetas", "Cocina")]
    [InlineData("132871", "Concursos", "Concurso")]
    public void ResolveKind_UsesTheTypeId(string id, string type, string expected)
    {
        Assert.Equal(expected, ProgramClassifier.ResolveKind(id, type));
    }

    [Theory]
    [InlineData("999999", "Serie Documental", "Documental")]
    [InlineData("999999", "Documental Original", "Documental")]
    [InlineData("999999", "SERIES INTERNACIONALES", "Serie")]
    [InlineData("999999", "Películas de cine", "Cine")]
    [InlineData("999999", "Magacín de tarde", "Magacín")]
    [InlineData("999999", "Conciertos", "Música")]
    public void ResolveKind_FallsBackToTheTypeName(string id, string type, string expected)
    {
        Assert.Equal(expected, ProgramClassifier.ResolveKind(id, type));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("999999", "Programa sin clasificar")]
    public void ResolveKind_ReturnsNullWhenTheTypeIsUnknown(string? id, string? type)
    {
        Assert.Null(ProgramClassifier.ResolveKind(id, type));
    }

    [Fact]
    public void ResolveKind_CoversEveryTypeSeenInTheCatalog()
    {
        (string Id, string Type)[] types =
        [
            ("132531", "Magacines diarios"),
            ("132532", "Entretenimiento"),
            ("132533", "Magacines"),
            ("132534", "Informativos Noticias"),
            ("132535", "Series"),
            ("132536", "Serie Documental"),
            ("132537", "Informativo diario"),
            ("132870", "Series Diarias"),
            ("132871", "Concursos"),
            ("136519", "Series Prime Time"),
            ("136520", "Series de Archivo"),
            ("136521", "Reportajes Factual"),
            ("136522", "Especial Evento"),
            ("136523", "Series Ficción Originales"),
            ("136524", "Talent"),
            ("136525", "Entrevistas"),
            ("136526", "Late Night"),
            ("136527", "Recetas"),
            ("136611", "Series Internacionales"),
            ("136612", "Magacín semanal"),
            ("136613", "Conciertos"),
            ("137310", "Contenedor Películas"),
            ("137650", "Documental"),
            ("141550", "Documental Original"),
            ("145350", "Infantiles"),
            ("1001560", "Videopodcast")
        ];

        foreach (var (id, type) in types)
        {
            var byId = ProgramClassifier.ResolveKind(id, type);

            Assert.NotNull(byId);
            Assert.Equal(byId, ProgramClassifier.ResolveKind(null, type));
        }
    }

    [Fact]
    public void Classify_PutsTheKindFirstAndKeepsTheRtveCategories()
    {
        var result = ProgramClassifier.Classify("132536", "Serie Documental", ["Cultura", "Humor"]);

        Assert.Equal("Documental", result.Kind);
        Assert.Equal(["Documental", "Cultura", "Humor"], result.Genres);
        Assert.Equal(["Serie Documental"], result.Tags);
    }

    [Fact]
    public void Classify_DeduplicatesCategories()
    {
        var result = ProgramClassifier.Classify("145350", "Infantiles", ["Infantil", "infantil", " "]);

        Assert.Equal(["Infantil"], result.Genres);
    }

    [Fact]
    public void Classify_DecodesTheHtmlEntitiesOfTheCategories()
    {
        var result = ProgramClassifier.Classify(null, null, ["Moda, famosos y tendencias &amp; humor"]);

        Assert.Equal(["Moda, famosos y tendencias & humor"], result.Genres);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public void Classify_ReturnsNothingWhenRtvePublishesNothing()
    {
        var result = ProgramClassifier.Classify(null, null, null);

        Assert.Null(result.Kind);
        Assert.Empty(result.Genres);
        Assert.Empty(result.Tags);
    }
}
