// CatalogService.cs
// Handles loading the app catalog from a remote JSON (GitHub raw), with local cache
// and embedded JSON fallback. Ensures the Store always has data even when offline.
//
// Flow: Remote JSON (5s timeout) → Local cache (%APPDATA%/Initio) → Embedded JSON resource

using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using NewPCSetupWPF.Models;

namespace NewPCSetupWPF.Services;

/// <summary>
/// Loads the store catalog from remote GitHub JSON, local cache, or embedded JSON fallback.
/// Single source of truth: catalog.json (same file for remote, cache, and embedded resource).
/// </summary>
public static class CatalogService
{
    /// <summary>GitHub raw URL for the catalog JSON file.</summary>
    private const string RemoteUrl =
        "https://raw.githubusercontent.com/UriCaBa/Initio/main/catalog.json";

    /// <summary>Logical name of the embedded JSON resource in the assembly.</summary>
    private const string EmbeddedResourceName = "catalog.json";

    /// <summary>Maximum time to wait for the remote catalog before falling back.</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Local cache file path: %APPDATA%/Initio/catalog_cache.json</summary>
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Initio", "catalog_cache.json");

    /// <summary>
    /// Loads the catalog: tries remote first, then cache, then embedded JSON resource.
    /// Returns a tuple with the list of items and the source label for logging.
    /// </summary>
    public static async Task<(IReadOnlyList<StoreTrendItem> Items, string Source)> LoadAsync()
    {
        // 1. Try remote
        try
        {
            using var http = new HttpClient { Timeout = DownloadTimeout };
            var json = await http.GetStringAsync(RemoteUrl).ConfigureAwait(false);
            var items = ParseCatalogJson(json);

            if (items.Count > 0)
            {
                // Cache the successful download for offline use
                await SaveCacheAsync(json).ConfigureAwait(false);
                return (items, "remote");
            }
        }
        catch
        {
            // Network error, timeout, or parse error — fall through to cache
        }

        // 2. Try local cache
        try
        {
            if (File.Exists(CachePath))
            {
                var cachedJson = await File.ReadAllTextAsync(CachePath).ConfigureAwait(false);
                var items = ParseCatalogJson(cachedJson);
                if (items.Count > 0)
                    return (items, "cache");
            }
        }
        catch
        {
            // Corrupted cache — fall through to embedded fallback
        }

        // 3. Embedded JSON resource (compiled into the .exe)
        return (LoadEmbeddedCatalog(), "embedded");
    }

    /// <summary>
    /// Loads the catalog from the embedded JSON resource compiled into the assembly.
    /// This is the ultimate fallback and always works, even fully offline on first launch.
    /// </summary>
    public static IReadOnlyList<StoreTrendItem> LoadEmbeddedCatalog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);

        if (stream is null)
            return Array.Empty<StoreTrendItem>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return ParseCatalogJson(json);
    }

    /// <summary>
    /// Parses the catalog JSON into a list of StoreTrendItem objects.
    /// Assigns rank and popularity signal based on position within each category.
    /// </summary>
    private static IReadOnlyList<StoreTrendItem> ParseCatalogJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var items = new List<StoreTrendItem>(200);

        if (!root.TryGetProperty("categories", out var categoriesElement))
            return items;

        foreach (var category in categoriesElement.EnumerateArray())
        {
            var categoryName = category.GetProperty("name").GetString() ?? "Unknown";

            if (!category.TryGetProperty("apps", out var appsElement))
                continue;

            int rank = 0;
            foreach (var app in appsElement.EnumerateArray())
            {
                rank++;
                var name = app.GetProperty("name").GetString() ?? "Unknown";
                var wingetId = app.GetProperty("wingetId").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(wingetId)) continue;

                // Assign popularity signal based on rank position (order in JSON = popularity)
                var signal = rank switch
                {
                    <= 3 => "Top ranked",
                    <= 10 => "Top free",
                    <= 20 => "Rising",
                    _ => "New"
                };
                var rating = Math.Max(3.5, Math.Round(4.9 - ((rank - 1) * 0.04), 1));

                items.Add(new StoreTrendItem(categoryName, rank, name, wingetId, rating, signal));
            }
        }

        return items;
    }

    /// <summary>Saves the raw JSON to the local cache file for offline use.</summary>
    private static async Task SaveCacheAsync(string json)
    {
        try
        {
            var cacheDir = Path.GetDirectoryName(CachePath)!;
            Directory.CreateDirectory(cacheDir);
            await File.WriteAllTextAsync(CachePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Non-critical — if cache write fails, we just won't have a cache next time
        }
    }
}
