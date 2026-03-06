using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace Initio.Core.Services;

public sealed class CatalogService : ICatalogService
{
    public const string DefaultRemoteUrl = "https://raw.githubusercontent.com/UriCaBa/Initio/main/catalog.json";
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly string _remoteUrl;
    private readonly string _cachePath;
    private readonly string _embeddedCatalogJson;
    private readonly HashSet<string> _trustedCatalogHashes;

    public CatalogService(
        string embeddedCatalogJson,
        HttpClient? httpClient = null,
        string? remoteUrl = null,
        string? cachePath = null,
        IEnumerable<string>? trustedCatalogHashes = null)
    {
        _embeddedCatalogJson = embeddedCatalogJson;
        _remoteUrl = remoteUrl ?? DefaultRemoteUrl;
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Initio",
            "catalog_cache.json");
        _httpClient = httpClient ?? new HttpClient { Timeout = DownloadTimeout };
        _trustedCatalogHashes = BuildTrustedHashSet(embeddedCatalogJson, trustedCatalogHashes);
    }

    public IReadOnlyList<StoreCatalogEntry> LoadEmbeddedCatalog()
    {
        if (string.IsNullOrWhiteSpace(_embeddedCatalogJson))
        {
            return Array.Empty<StoreCatalogEntry>();
        }

        try
        {
            return CatalogJsonParser.Parse(_embeddedCatalogJson);
        }
        catch
        {
            return Array.Empty<StoreCatalogEntry>();
        }
    }

    public async Task<CatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        List<string> diagnostics = [];

        var remoteItems = await TryLoadRemoteAsync(diagnostics, cancellationToken).ConfigureAwait(false);
        if (remoteItems.Count > 0)
        {
            return new CatalogLoadResult(remoteItems, "remote", diagnostics);
        }

        var cachedItems = await TryLoadCacheAsync(diagnostics, cancellationToken).ConfigureAwait(false);
        if (cachedItems.Count > 0)
        {
            return new CatalogLoadResult(cachedItems, "cache", diagnostics);
        }

        var embeddedItems = LoadEmbeddedCatalog();
        if (embeddedItems.Count == 0)
        {
            diagnostics.Add("Embedded catalog is empty or invalid.");
        }

        return new CatalogLoadResult(embeddedItems, "embedded", diagnostics);
    }

    public static string ComputeSha256(string content)
    {
        var normalized = (content ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task<IReadOnlyList<StoreCatalogEntry>> TryLoadRemoteAsync(List<string> diagnostics, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DownloadTimeout);
            using var response = await _httpClient.GetAsync(_remoteUrl, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            if (!TryParseTrustedCatalog(json, "Remote", diagnostics, out var items))
            {
                return Array.Empty<StoreCatalogEntry>();
            }

            var cacheError = await SaveCacheAsync(json, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cacheError))
            {
                diagnostics.Add(cacheError);
            }

            return items;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Remote catalog failed: {ex.Message}");
            return Array.Empty<StoreCatalogEntry>();
        }
    }

    private async Task<IReadOnlyList<StoreCatalogEntry>> TryLoadCacheAsync(List<string> diagnostics, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                diagnostics.Add("Catalog cache not found.");
                return Array.Empty<StoreCatalogEntry>();
            }

            var cachedJson = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            if (!TryParseTrustedCatalog(cachedJson, "Cache", diagnostics, out var items))
            {
                return Array.Empty<StoreCatalogEntry>();
            }

            return items;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Catalog cache failed: {ex.Message}");
            return Array.Empty<StoreCatalogEntry>();
        }
    }

    private bool TryParseTrustedCatalog(string json, string source, List<string> diagnostics, out IReadOnlyList<StoreCatalogEntry> items)
    {
        items = Array.Empty<StoreCatalogEntry>();
        if (string.IsNullOrWhiteSpace(json))
        {
            diagnostics.Add($"{source} catalog was empty.");
            return false;
        }

        var hash = ComputeSha256(json);
        if (_trustedCatalogHashes.Count > 0 && !_trustedCatalogHashes.Contains(hash))
        {
            diagnostics.Add($"{source} catalog rejected: SHA-256 {hash[..12]} is not trusted.");
            return false;
        }

        try
        {
            var parsedItems = CatalogJsonParser.Parse(json);
            if (parsedItems.Count == 0)
            {
                diagnostics.Add($"{source} catalog was trusted but contained no apps.");
                return false;
            }

            items = parsedItems;
            return true;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"{source} catalog parse failed: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> SaveCacheAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            var cacheDirectory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            await File.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return $"Catalog cache write failed: {ex.Message}";
        }
    }

    private static HashSet<string> BuildTrustedHashSet(string embeddedCatalogJson, IEnumerable<string>? trustedCatalogHashes)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (trustedCatalogHashes is not null)
        {
            foreach (var hash in trustedCatalogHashes)
            {
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    hashes.Add(hash.Trim());
                }
            }
        }

        if (hashes.Count == 0 && !string.IsNullOrWhiteSpace(embeddedCatalogJson))
        {
            hashes.Add(ComputeSha256(embeddedCatalogJson));
        }

        return hashes;
    }
}
