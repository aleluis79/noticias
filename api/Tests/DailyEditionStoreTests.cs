using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewsAggregator.Models;
using NewsAggregator.Services.Archive;
using Xunit;

public class DailyEditionStoreTests
{
    [Fact]
    public async Task SaveAsync_ShouldPersistMetadataAndReadItBack()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            var date = new DateOnly(2026, 11, 17);
            var articles = new[]
            {
                Article("Primera nota del día"),
                Article("Segunda nota del día")
            };

            await store.SaveAsync(date, articles, new DateTime(2026, 11, 17, 22, 0, 0, DateTimeKind.Utc));
            var stored = await store.GetAsync(date);

            Assert.NotNull(stored);
            Assert.Equal(date, stored!.Date);
            Assert.Equal(2, stored.Articles.Count);
            Assert.Equal("Primera nota del día", stored.Articles[0].Title);

            var json = await File.ReadAllTextAsync(Path.Combine(directory, "2026-11-17.json"));
            Assert.Contains("\"articles\"", json);
            Assert.DoesNotContain("paragraphs", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"body\"", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldKeepOnlyTheRetentionWindow()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);

            foreach (var day in new[] { 14, 15, 16, 17 })
            {
                await store.SaveAsync(
                    new DateOnly(2026, 11, day),
                    new[] { Article($"Nota del {day}") },
                    new DateTime(2026, 11, day, 12, 0, 0, DateTimeKind.Utc));
            }

            var files = Directory.GetFiles(directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name)
                .ToArray();

            Assert.Equal(new[] { "2026-11-15", "2026-11-16", "2026-11-17" }, files);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetPreviousAsync_ShouldReturnStoredDaysNewestFirst()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);
            await store.SaveAsync(
                new DateOnly(2026, 11, 15),
                new[] { Article("Anteayer") },
                new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
            await store.SaveAsync(
                new DateOnly(2026, 11, 16),
                new[] { Article("Ayer") },
                new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc));

            var previous = await store.GetPreviousAsync(new DateOnly(2026, 11, 17), 2);

            Assert.Equal(2, previous.Count);
            Assert.Equal(new DateOnly(2026, 11, 16), previous[0].Date);
            Assert.Equal(new DateOnly(2026, 11, 15), previous[1].Date);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_ShouldIgnoreCorruptFiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "2026-11-16.json"),
                "{ not valid json");
            var store = CreateStore(directory);

            var stored = await store.GetAsync(new DateOnly(2026, 11, 16));

            Assert.Null(stored);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldIgnoreEmptyEditions()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = CreateStore(directory);

            await store.SaveAsync(
                new DateOnly(2026, 11, 17),
                Array.Empty<NewsArticle>(),
                new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc));

            Assert.Empty(Directory.GetFiles(directory, "*.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DailyEditionStore CreateStore(string directory) =>
        new(
            Options.Create(new ArchiveOptions { Directory = directory, RetentionDays = 3 }),
            NullLogger<DailyEditionStore>.Instance);

    private static NewsArticle Article(string title) => new()
    {
        Title = title,
        Summary = "Resumen de la nota.",
        OriginalUrl = $"https://www.example.com/nota-{title.Length}",
        SourceName = "Test News"
    };

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"noticias-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
