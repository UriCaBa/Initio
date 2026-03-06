using System.Net;
using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace Initio.Tests;

internal static class TestData
{
    public static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string LoadRepoCatalogJson()
    {
        return File.ReadAllText(Path.Combine(RepoRoot, "catalog.json"));
    }

    public static IReadOnlyList<StoreCatalogEntry> CreateStoreItems()
    {
        return
        [
            new("Browsers", 1, "Google Chrome", "Google.Chrome", 4.8, "Top ranked"),
            new("Browsers", 2, "Mozilla Firefox", "Mozilla.Firefox", 4.7, "Top free"),
            new("Media", 1, "Spotify", "Spotify.Spotify", 4.6, "Top ranked"),
            new("Media", 2, "VLC", "VideoLAN.VLC", 4.5, "Top free"),
            new("Communication", 1, "Discord", "Discord.Discord", 4.8, "Top ranked"),
            new("Gaming", 1, "Steam", "Valve.Steam", 4.9, "Top ranked"),
            new("Cloud", 1, "Dropbox", "Dropbox.Dropbox", 4.4, "Top free"),
            new("Docs", 1, "LibreOffice", "TheDocumentFoundation.LibreOffice", 4.3, "Top free"),
            new("Drivers", 1, "NVIDIA App", "Nvidia.NVIDIAApp", 4.4, "Top ranked"),
            new("Utilities", 1, "Notepad++", "Notepad++.Notepad++", 4.6, "Top free"),
            new("Gaming", 2, "Riot Client", "RiotGames.RiotClient", 4.4, "Top free"),
            new("Docs", 2, "Foxit PDF Reader", "Foxit.FoxitReader", 4.2, "Rising"),
            new("Development", 1, "Visual Studio Code", "Microsoft.VisualStudioCode", 4.9, "Top ranked"),
            new("Development", 2, "Git", "Git.Git", 4.7, "Top free"),
            new("Development", 3, "Docker Desktop", "Docker.DockerDesktop", 4.5, "Rising"),
            new("Streaming", 1, "OBS Studio", "OBSProject.OBSStudio", 4.6, "Top ranked"),
            new("Development", 4, "GitHub Desktop", "GitHub.GitHubDesktop", 4.3, "Rising"),
            new("Office", 1, "Slack", "SlackTechnologies.Slack", 4.4, "Top free"),
            new("Meetings", 1, "Zoom", "Zoom.Zoom", 4.3, "Top free"),
            new("Office", 2, "Microsoft Teams", "Microsoft.Teams", 4.1, "Rising"),
            new("Productivity", 1, "Notion", "Notion.Notion", 4.2, "Rising"),
            new("Gaming", 3, "Epic Games Launcher", "EpicGames.EpicGamesLauncher", 4.2, "Rising"),
            new("Gaming", 4, "GOG Galaxy", "GOG.Galaxy", 4.0, "New")
        ];
    }

    public static IReadOnlyList<BloatwareDefinition> CreateBloatwareItems()
    {
        return
        [
            new("Candy Crush Saga", "Games", "king.com.CandyCrushSaga", "Pre-installed game"),
            new("TikTok", "Social & Entertainment", "BytedancePte.Ltd.TikTok", "Pre-installed social app"),
            new("Xbox Game Bar", "Promotions", "Microsoft.XboxGamingOverlay", "Gaming overlay")
        ];
    }
}

internal sealed class StubCatalogService : ICatalogService
{
    private readonly IReadOnlyList<StoreCatalogEntry> _items;
    private readonly string _source;

    public StubCatalogService(IReadOnlyList<StoreCatalogEntry>? items = null, string source = "fake")
    {
        _items = items ?? TestData.CreateStoreItems();
        _source = source;
    }

    public IReadOnlyList<StoreCatalogEntry> LoadEmbeddedCatalog()
    {
        return _items.ToList();
    }

    public Task<CatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CatalogLoadResult(LoadEmbeddedCatalog(), _source, Array.Empty<string>()));
    }
}

internal sealed class StubWingetClient : IWingetClient
{
    private readonly Dictionary<string, Queue<string?>> _installResponses = new(StringComparer.OrdinalIgnoreCase);

    public StubWingetClient()
    {
        InstalledTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google.Chrome",
            "Google Chrome",
            "Spotify.Spotify",
            "Spotify",
            "Notepad++.Notepad++",
            "Notepad++"
        };
        SearchResults =
        [
            new WingetSearchResult("Visual Studio Code", "Microsoft.VisualStudioCode"),
            new WingetSearchResult("Git", "Git.Git"),
            new WingetSearchResult("OBS Studio", "OBSProject.OBSStudio"),
            new WingetSearchResult("Docker Desktop", "Docker.DockerDesktop")
        ];
    }

    public string? Version { get; set; } = "v1.8.10291";
    public HashSet<string> InstalledTokens { get; }
    public IReadOnlyList<WingetSearchResult> SearchResults { get; set; }
    public List<string> InstallRequests { get; } = [];
    public Func<string?, CancellationToken, Task<string?>>? ListInstalledHandler { get; set; }
    public Func<string, int, CancellationToken, Task<IReadOnlyList<WingetSearchResult>>>? SearchHandler { get; set; }
    public Func<string, bool, TimeSpan, CancellationToken, Task<string?>>? InstallHandler { get; set; }

    public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Version);
    }

    public Task<string?> ListInstalledAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        if (ListInstalledHandler is not null)
        {
            return ListInstalledHandler(query, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<string?>(string.Join(Environment.NewLine, InstalledTokens));
        }

        var matches = InstalledTokens.Where(token => token.Contains(query, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<string?>(string.Join(Environment.NewLine, matches));
    }

    public Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (SearchHandler is not null)
        {
            return SearchHandler(query, maxResults, cancellationToken);
        }

        var results = SearchResults
            .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || item.WingetId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
        return Task.FromResult<IReadOnlyList<WingetSearchResult>>(results);
    }

    public async Task<string?> InstallAsync(string wingetId, bool silent, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        InstallRequests.Add(wingetId);

        if (InstallHandler is not null)
        {
            return await InstallHandler(wingetId, silent, timeout, cancellationToken);
        }

        if (_installResponses.TryGetValue(wingetId, out var responses) && responses.Count > 0)
        {
            var response = responses.Dequeue();
            TrackSuccessfulInstall(wingetId, response);
            return response;
        }

        const string success = "Successfully installed";
        TrackSuccessfulInstall(wingetId, success);
        return success;
    }

    public void QueueInstallResponses(string wingetId, params string?[] responses)
    {
        _installResponses[wingetId] = new Queue<string?>(responses);
    }

    public int GetInstallAttempts(string wingetId)
    {
        return InstallRequests.Count(request => string.Equals(request, wingetId, StringComparison.OrdinalIgnoreCase));
    }

    private void TrackSuccessfulInstall(string wingetId, string? response)
    {
        if (response is null)
        {
            return;
        }

        if (response.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Already installed", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("No available upgrade", StringComparison.OrdinalIgnoreCase))
        {
            InstalledTokens.Add(wingetId);
        }
    }
}

internal sealed class StubBloatwareService : IBloatwareService
{
    public StubBloatwareService(IReadOnlyList<BloatwareDefinition>? items = null)
    {
        Items = items ?? TestData.CreateBloatwareItems();
        InstalledPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "king.com.CandyCrushSaga",
            "BytedancePte.Ltd.TikTok"
        };
    }

    public IReadOnlyList<BloatwareDefinition> Items { get; }
    public HashSet<string> InstalledPackages { get; }
    public List<string> RemovedPackages { get; } = [];
    public Func<IReadOnlyCollection<string>, CancellationToken, Task<HashSet<string>>>? DetectHandler { get; set; }
    public Func<string, CancellationToken, Task<bool>>? RemoveHandler { get; set; }
    public Func<string, CancellationToken, Task<bool>>? VerifyHandler { get; set; }

    public IReadOnlyList<BloatwareDefinition> GetKnownBloatware()
    {
        return Items.ToList();
    }

    public Task<HashSet<string>> DetectInstalledAsync(IReadOnlyCollection<string> packageNames, CancellationToken cancellationToken = default)
    {
        if (DetectHandler is not null)
        {
            return DetectHandler(packageNames, cancellationToken);
        }

        return Task.FromResult(new HashSet<string>(InstalledPackages.Intersect(packageNames, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase));
    }

    public async Task<bool> RemovePackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (RemoveHandler is not null)
        {
            return await RemoveHandler(packageName, cancellationToken);
        }

        RemovedPackages.Add(packageName);
        InstalledPackages.Remove(packageName);
        return true;
    }

    public Task<bool> VerifyRemovedAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (VerifyHandler is not null)
        {
            return VerifyHandler(packageName, cancellationToken);
        }

        return Task.FromResult(!InstalledPackages.Contains(packageName));
    }
}

internal sealed class RecordingProcessRunner : IProcessRunner
{
    public Queue<ProcessResult?> Results { get; } = new();
    public List<ProcessSpec> Calls { get; } = [];
    public Func<ProcessSpec, TimeSpan, CancellationToken, Task<ProcessResult?>>? Handler { get; set; }

    public Task<ProcessResult?> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Calls.Add(spec);
        if (Handler is not null)
        {
            return Handler(spec, timeout, cancellationToken);
        }

        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new ProcessResult(0, string.Empty, string.Empty));
    }
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}

internal static class AsyncTestHelper
{
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000, int pollMs = 25)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollMs);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
    }
}
