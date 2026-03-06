using System.Net;
using System.Net.Http;
using Initio.Core.Services;

namespace Initio.Tests;

public class CatalogServiceTests
{
    [Fact]
    public void LoadEmbeddedCatalog_FromRepoCatalog_ReturnsExpectedStructure()
    {
        var service = new CatalogService(TestData.LoadRepoCatalogJson());

        var items = service.LoadEmbeddedCatalog();

        Assert.NotEmpty(items);
        Assert.True(items.Count >= 150, $"Expected at least 150 apps, got {items.Count}");
        Assert.Contains(items, item => item.WingetId == "Microsoft.PowerToys");
        Assert.Contains(items, item => item.WingetId == "Mozilla.Firefox");
        Assert.Contains(items.Select(item => item.Category).Distinct(), category => category == "Development");
        Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(item.WingetId)));
    }

    [Fact]
    public async Task LoadAsync_UsesRemoteCatalogAndWritesCache()
    {
        var remoteJson = TestData.LoadRepoCatalogJson();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(remoteJson)
            }));
        var cachePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog_cache.json");
        using var httpClient = new HttpClient(handler);
        var service = new CatalogService(
            "{\"categories\":[]}",
            httpClient,
            cachePath: cachePath,
            trustedCatalogHashes: [CatalogService.ComputeSha256(remoteJson)]);

        var result = await service.LoadAsync();

        Assert.Equal("remote", result.Source);
        Assert.NotEmpty(result.Items);
        Assert.True(File.Exists(cachePath));
        Assert.Equal(remoteJson, await File.ReadAllTextAsync(cachePath));
        Assert.DoesNotContain(result.Diagnostics, message => message.Contains("rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_FallsBackToCacheWhenRemoteFails()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, "catalog_cache.json");
        var cachedJson = TestData.LoadRepoCatalogJson();
        await File.WriteAllTextAsync(cachePath, cachedJson);

        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));
        using var httpClient = new HttpClient(handler);
        var service = new CatalogService(
            "{\"categories\":[]}",
            httpClient,
            cachePath: cachePath,
            trustedCatalogHashes: [CatalogService.ComputeSha256(cachedJson)]);

        var result = await service.LoadAsync();

        Assert.Equal("cache", result.Source);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, item => item.WingetId == "Git.Git");
        Assert.Contains(result.Diagnostics, message => message.Contains("Remote catalog failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_RejectsUntrustedRemoteCatalogAndFallsBackToEmbedded()
    {
        var embeddedJson = TestData.LoadRepoCatalogJson();
        var tamperedJson = embeddedJson + " ";
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tamperedJson)
            }));
        var cachePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog_cache.json");
        using var httpClient = new HttpClient(handler);
        var service = new CatalogService(embeddedJson, httpClient, cachePath: cachePath);

        var result = await service.LoadAsync();

        Assert.Equal("embedded", result.Source);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, item => item.WingetId == "Mozilla.Firefox");
        Assert.Contains(result.Diagnostics, message => message.Contains("not trusted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_RejectsRemoteCatalogWhenNoTrustedHashesConfigured()
    {
        const string remoteJson = """
{"categories":[{"name":"Utilities","apps":[{"name":"PowerToys","wingetId":"Microsoft.PowerToys"}]}]}
""";
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(remoteJson)
            }));
        using var httpClient = new HttpClient(handler);
        var service = new CatalogService(string.Empty, httpClient, cachePath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catalog_cache.json"));

        var result = await service.LoadAsync();

        Assert.Equal("embedded", result.Source);
        Assert.Empty(result.Items);
        Assert.Contains(result.Diagnostics, message => message.Contains("no trusted catalog hashes configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_FallsBackToEmbeddedWhenRemoteAndCacheFail()
    {
        var embeddedJson = TestData.LoadRepoCatalogJson();
        var cacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, "catalog_cache.json");
        await File.WriteAllTextAsync(cachePath, "{not-valid-json}");

        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var httpClient = new HttpClient(handler);
        var service = new CatalogService(embeddedJson, httpClient, cachePath: cachePath);

        var result = await service.LoadAsync();

        Assert.Equal("embedded", result.Source);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, item => item.WingetId == "Valve.Steam");
        Assert.Contains(result.Diagnostics, message => message.Contains("Remote catalog failed", StringComparison.OrdinalIgnoreCase));
    }
}
