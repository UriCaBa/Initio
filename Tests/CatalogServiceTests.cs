// CatalogServiceTests.cs
// Unit tests for CatalogService — validates JSON parsing, embedded resource loading,
// and the full async load flow (remote → cache → embedded fallback).

using NewPCSetupWPF.Services;
using NewPCSetupWPF.Models;

namespace Initio.Tests;

/// <summary>
/// Tests for CatalogService: embedded catalog loading, JSON parsing, and async flow.
/// </summary>
public class CatalogServiceTests
{
    // ═══ Embedded Resource Loading ═══

    [Fact]
    public void LoadEmbeddedCatalog_ReturnsNonEmptyList()
    {
        var items = CatalogService.LoadEmbeddedCatalog();

        Assert.NotNull(items);
        Assert.NotEmpty(items);
    }

    [Fact]
    public void LoadEmbeddedCatalog_ContainsExpectedCategories()
    {
        var items = CatalogService.LoadEmbeddedCatalog();
        var categories = items.Select(i => i.Category).Distinct().ToList();

        // The catalog.json has 7 categories
        Assert.Contains("Productivity", categories);
        Assert.Contains("Communication", categories);
        Assert.Contains("Media & Creativity", categories);
        Assert.Contains("Development", categories);
        Assert.Contains("Gaming", categories);
        Assert.Contains("Security & Privacy", categories);
        Assert.Contains("System Utilities", categories);
        Assert.Equal(7, categories.Count);
    }

    [Fact]
    public void LoadEmbeddedCatalog_EachItemHasValidProperties()
    {
        var items = CatalogService.LoadEmbeddedCatalog();

        foreach (var item in items)
        {
            // Every item must have a non-empty name and wingetId
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"Item has empty Name (WingetId: {item.WingetId})");
            Assert.False(string.IsNullOrWhiteSpace(item.WingetId), $"Item has empty WingetId (Name: {item.Name})");
            Assert.False(string.IsNullOrWhiteSpace(item.Category), $"Item has empty Category (Name: {item.Name})");

            // Rank should be positive
            Assert.True(item.Rank > 0, $"Item {item.Name} has invalid Rank: {item.Rank}");

            // Rating should be between 3.5 and 5.0
            Assert.InRange(item.Rating, 3.5, 5.0);

            // PopularitySignal should be one of the expected values
            Assert.Contains(item.PopularitySignal, new[] { "Top ranked", "Top free", "Rising", "New" });
        }
    }

    [Fact]
    public void LoadEmbeddedCatalog_HasMinimumAppCount()
    {
        var items = CatalogService.LoadEmbeddedCatalog();

        // We expect ~200 apps across all categories (at least 150 as a safety margin)
        Assert.True(items.Count >= 150, $"Expected at least 150 apps, got {items.Count}");
    }

    [Fact]
    public void LoadEmbeddedCatalog_EachCategoryHasApps()
    {
        var items = CatalogService.LoadEmbeddedCatalog();
        var grouped = items.GroupBy(i => i.Category);

        foreach (var group in grouped)
        {
            // Each category should have at least 10 apps
            Assert.True(group.Count() >= 10,
                $"Category '{group.Key}' only has {group.Count()} apps, expected at least 10");
        }
    }

    [Fact]
    public void LoadEmbeddedCatalog_RanksAreSequentialPerCategory()
    {
        var items = CatalogService.LoadEmbeddedCatalog();
        var grouped = items.GroupBy(i => i.Category);

        foreach (var group in grouped)
        {
            var ranks = group.Select(i => i.Rank).ToList();
            // Ranks should start at 1 and be sequential
            for (int i = 0; i < ranks.Count; i++)
            {
                Assert.Equal(i + 1, ranks[i]);
            }
        }
    }

    // ═══ Data Integrity ═══

    [Fact]
    public void LoadEmbeddedCatalog_NoDuplicateWingetIds()
    {
        var items = CatalogService.LoadEmbeddedCatalog();
        var duplicates = items.GroupBy(i => i.WingetId, StringComparer.OrdinalIgnoreCase)
                              .Where(g => g.Count() > 1)
                              .Select(g => g.Key)
                              .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void LoadEmbeddedCatalog_AllWingetIdsMatchValidationPattern()
    {
        var items = CatalogService.LoadEmbeddedCatalog();
        var invalid = items.Where(i => !NewPCSetupWPF.Services.InputValidation.IsValidWingetId(i.WingetId))
                           .Select(i => $"{i.Name}: {i.WingetId}")
                           .ToList();

        Assert.Empty(invalid);
    }

    // ═══ Well-Known Apps ═══

    [Theory]
    [InlineData("Microsoft.PowerToys")]
    [InlineData("Mozilla.Firefox")]
    [InlineData("Discord.Discord")]
    [InlineData("Valve.Steam")]
    [InlineData("Microsoft.VisualStudioCode")]
    [InlineData("Git.Git")]
    [InlineData("Spotify.Spotify")]
    [InlineData("VideoLAN.VLC")]
    public void LoadEmbeddedCatalog_ContainsWellKnownApps(string wingetId)
    {
        var items = CatalogService.LoadEmbeddedCatalog();

        Assert.Contains(items, i =>
            string.Equals(i.WingetId, wingetId, StringComparison.OrdinalIgnoreCase));
    }

    // ═══ StoreTrendItem Model ═══

    [Fact]
    public void StoreTrendItem_IsSelectedDefaultsFalse()
    {
        var item = new StoreTrendItem("Test", 1, "TestApp", "Test.App", 4.5, "Top ranked");

        Assert.False(item.IsSelected);
    }

    [Fact]
    public void StoreTrendItem_CatalogStatusDefaultsEmpty()
    {
        var item = new StoreTrendItem("Test", 1, "TestApp", "Test.App", 4.5, "Top ranked");

        Assert.Equal(string.Empty, item.CatalogStatus);
    }

    [Fact]
    public void StoreTrendItem_PropertyChangedFires()
    {
        var item = new StoreTrendItem("Test", 1, "TestApp", "Test.App", 4.5, "Top ranked");
        var changedProps = new List<string>();
        item.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

        item.IsSelected = true;
        item.CatalogStatus = "✅ Installed";

        Assert.Contains(nameof(StoreTrendItem.IsSelected), changedProps);
        Assert.Contains(nameof(StoreTrendItem.CatalogStatus), changedProps);
    }

    [Fact]
    public void StoreTrendItem_TrendScoreCalculation()
    {
        var top = new StoreTrendItem("Test", 1, "First", "Test.First", 4.9, "Top ranked");
        var mid = new StoreTrendItem("Test", 10, "Tenth", "Test.Tenth", 4.5, "Top free");
        var low = new StoreTrendItem("Test", 30, "Thirtieth", "Test.Thirtieth", 3.7, "New");

        // TrendScore = Max(42, 100 - (Rank-1)*2)
        Assert.Equal(100, top.TrendScore);   // 100 - 0 = 100
        Assert.Equal(82, mid.TrendScore);    // 100 - 18 = 82
        Assert.Equal(42, low.TrendScore);    // 100 - 58 = 42 (clamped)
    }

    [Fact]
    public void StoreTrendItem_TrendScoreDifferentiatesAllRanks()
    {
        // Verify that ranks 1-30 all produce distinct scores
        var scores = Enumerable.Range(1, 30)
            .Select(r => new StoreTrendItem("Test", r, $"App{r}", $"Test.App{r}", 4.0, "Test").TrendScore)
            .ToList();

        Assert.Equal(30, scores.Distinct().Count());
    }

    // ═══ Model SetField ═══

    [Fact]
    public void AppItem_SetFieldWithBool_NoBoxingIssue()
    {
        var item = new NewPCSetupWPF.Models.AppItem { Name = "Test", Category = "Test", WingetId = "Test.Test" };
        var changedProps = new List<string>();
        item.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName!);

        item.IsSelected = true;
        item.IsSelected = true; // same value — should NOT fire

        Assert.Single(changedProps, nameof(NewPCSetupWPF.Models.AppItem.IsSelected));
    }

    // ═══ Async Load Flow ═══

    [Fact]
    public async Task LoadAsync_ReturnsValidResult()
    {
        // LoadAsync always returns something (remote, cache, or embedded)
        var (items, source) = await CatalogService.LoadAsync();

        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.Contains(source, new[] { "remote", "cache", "embedded" });
    }
}
