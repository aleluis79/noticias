using System.Text.Json;
using NewsAggregator.Models;
using NewsAggregator.Services.Grouping;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Banco de pruebas del agrupamiento sobre capturas reales del feed.
///
/// Cada captura es un día de titulares en api/Tests/Fixtures/clustering. Los
/// pares de labels.json son el contrato: los "same" tienen que quedar en la
/// misma historia y los "different" en historias distintas. El umbral de
/// producción no se mueve sin pasar por acá.
/// </summary>
public class ClusteringCorpusTests
{
    private static readonly string FixtureDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Tests",
        "Fixtures",
        "clustering");

    private readonly ITestOutputHelper _output;

    public ClusteringCorpusTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed record Snapshot(string Name, IReadOnlyList<NewsArticle> Articles);

    private sealed record LabelPair(string A, string B, string Why);

    private sealed record Labels(
        IReadOnlyList<LabelPair> Same,
        IReadOnlyList<LabelPair> Different);

    private static IReadOnlyList<Snapshot> LoadSnapshots()
    {
        var snapshots = new List<Snapshot>();

        foreach (var file in Directory.GetFiles(FixtureDirectory, "*.json"))
        {
            if (Path.GetFileName(file) == "labels.json")
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var articles = new List<NewsArticle>();

            foreach (var element in document.RootElement.GetProperty("articles").EnumerateArray())
            {
                articles.Add(new NewsArticle
                {
                    SourceName = element.GetProperty("sourceName").GetString() ?? string.Empty,
                    Title = element.GetProperty("title").GetString() ?? string.Empty,
                    OriginalUrl = $"https://example.test/{articles.Count}"
                });
            }

            snapshots.Add(new Snapshot(Path.GetFileNameWithoutExtension(file), articles));
        }

        return snapshots;
    }

    private static Labels LoadLabels()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDirectory, "labels.json")));

        static List<LabelPair> Read(JsonElement root, string property) =>
            root.GetProperty(property)
                .EnumerateArray()
                .Select(element => new LabelPair(
                    element.GetProperty("a").GetString() ?? string.Empty,
                    element.GetProperty("b").GetString() ?? string.Empty,
                    element.GetProperty("why").GetString() ?? string.Empty))
                .ToList();

        var root = document.RootElement;
        return new Labels(Read(root, "same"), Read(root, "different"));
    }

    private static StoryGroupingService CreateService(double threshold) =>
        new(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StoryGroupingService>.Instance,
            threshold);

    private static bool ShareStory(
        IReadOnlyList<NewsStory> stories,
        string titleA,
        string titleB)
    {
        foreach (var story in stories)
        {
            var hasA = story.Sources.Any(source => source.Title == titleA);
            var hasB = story.Sources.Any(source => source.Title == titleB);
            if (hasA && hasB)
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void Fixtures_LoadWithEnoughSources()
    {
        var snapshots = LoadSnapshots();

        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, snapshot =>
        {
            Assert.True(snapshot.Articles.Count > 100, $"{snapshot.Name} trae muy pocos artículos");
            Assert.True(
                snapshot.Articles.Select(article => article.SourceName).Distinct().Count() >= 8,
                $"{snapshot.Name} cubre menos de 8 medios");
        });
    }

    [Fact]
    public void Labels_MatchTheCapturedTitles()
    {
        var titles = LoadSnapshots()
            .SelectMany(snapshot => snapshot.Articles)
            .Select(article => article.Title)
            .ToHashSet(StringComparer.Ordinal);

        var labels = LoadLabels();

        foreach (var pair in labels.Same.Concat(labels.Different))
        {
            Assert.True(titles.Contains(pair.A), $"Título ausente en los fixtures: {pair.A}");
            Assert.True(titles.Contains(pair.B), $"Título ausente en los fixtures: {pair.B}");
        }
    }

    [Fact]
    public void ProductionThreshold_HonoursEveryLabel()
    {
        var labels = LoadLabels();
        var service = CreateService(StoryGroupingService.SameStoryThreshold);

        foreach (var snapshot in LoadSnapshots())
        {
            var stories = service.Group(snapshot.Articles);

            foreach (var pair in labels.Same)
            {
                Assert.True(
                    ShareStory(stories, pair.A, pair.B),
                    $"{snapshot.Name}: deberían compartir historia. {pair.Why}");
            }

            foreach (var pair in labels.Different)
            {
                Assert.False(
                    ShareStory(stories, pair.A, pair.B),
                    $"{snapshot.Name}: no deberían compartir historia. {pair.Why}");
            }
        }
    }

    [Theory]
    [InlineData(0.50)]
    [InlineData(0.52)]
    [InlineData(0.55)]
    [InlineData(0.60)]
    public void RaisingTheThreshold_StillKeepsDifferentStoriesApart(double threshold)
    {
        var labels = LoadLabels();
        var service = CreateService(threshold);

        foreach (var snapshot in LoadSnapshots())
        {
            var stories = service.Group(snapshot.Articles);

            foreach (var pair in labels.Different)
            {
                Assert.False(
                    ShareStory(stories, pair.A, pair.B),
                    $"{snapshot.Name} @ {threshold}: se fusionaron historias distintas. {pair.Why}");
            }
        }
    }

    /// <summary>
    /// Writes clustering-report.txt beside the test binaries so a calibration
    /// run can compare snapshots without throwaway scripts. It fails nothing.
    /// </summary>
    [Fact]
    public void Report_ClusterVolumePerThreshold()
    {
        var snapshots = LoadSnapshots();
        double[] thresholds = { 0.45, 0.48, 0.50, 0.52, 0.55, 0.60 };
        var lines = new List<string>();

        foreach (var snapshot in snapshots)
        {
            lines.Add($"== {snapshot.Name} ({snapshot.Articles.Count} artículos)");

            foreach (var threshold in thresholds)
            {
                var stories = CreateService(threshold).Group(snapshot.Articles);
                var clustered = stories.Where(story => story.SourceCount > 1).ToList();

                lines.Add(
                    $"  {threshold:0.00}: {stories.Count} historias, " +
                    $"{clustered.Count} con más de un medio, " +
                    $"{clustered.Sum(story => story.ArticleCount)} notas agrupadas, " +
                    $"máx {stories.Max(story => story.SourceCount)} medios");
            }
        }

        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }

        var reportPath = Path.Combine(AppContext.BaseDirectory, "clustering-report.txt");
        File.WriteAllLines(reportPath, lines);
        _output.WriteLine($"Reporte escrito en {reportPath}");
    }
}
