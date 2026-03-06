using Initio.Core.Models;

namespace Initio.Core.Abstractions;

public interface IWingetClient
{
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
    Task<string?> ListInstalledAsync(string? query = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<string?> InstallAsync(string wingetId, bool silent, TimeSpan timeout, CancellationToken cancellationToken = default);
}
