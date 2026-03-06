using Initio.Core.Models;

namespace Initio.Core.Abstractions;

public interface ICatalogService
{
    IReadOnlyList<StoreCatalogEntry> LoadEmbeddedCatalog();
    Task<CatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record CatalogLoadResult(IReadOnlyList<StoreCatalogEntry> Items, string Source, IReadOnlyList<string> Diagnostics);
