using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace NewPCSetupWPF.Services;

public sealed class FakeWingetClient : IWingetClient
{
    private static readonly IReadOnlyList<WingetSearchResult> SampleResults =
    [
        new("Visual Studio Code", "Microsoft.VisualStudioCode"),
        new("Git", "Git.Git"),
        new("OBS Studio", "OBSProject.OBSStudio"),
        new("Docker Desktop", "Docker.DockerDesktop"),
        new("PowerToys", "Microsoft.PowerToys")
    ];

    public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>("v1.8.10291");
    }

    public Task<string?> ListInstalledAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        const string installed = "Google.Chrome\nSpotify.Spotify\nNotepad++.Notepad++\nGoogle Chrome\nSpotify\nNotepad++";
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<string?>(installed);
        }

        var matches = installed.Contains(query, StringComparison.OrdinalIgnoreCase) ? query : string.Empty;
        return Task.FromResult<string?>(matches);
    }

    public Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var filtered = SampleResults
            .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || item.WingetId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
        return Task.FromResult<IReadOnlyList<WingetSearchResult>>(filtered);
    }

    public Task<string?> InstallAsync(string wingetId, bool silent, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>("Successfully installed");
    }
}
