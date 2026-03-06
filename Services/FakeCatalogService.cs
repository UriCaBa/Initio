using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace NewPCSetupWPF.Services;

public sealed class FakeCatalogService : ICatalogService
{
    private static readonly IReadOnlyList<StoreCatalogEntry> SampleItems =
    [
        new("Browsers", 1, "Google Chrome", "Google.Chrome", 4.8, "Top ranked"),
        new("Browsers", 2, "Mozilla Firefox", "Mozilla.Firefox", 4.7, "Top free"),
        new("Development", 1, "Visual Studio Code", "Microsoft.VisualStudioCode", 4.9, "Top ranked"),
        new("Development", 2, "Git", "Git.Git", 4.7, "Top free"),
        new("Communication", 1, "Discord", "Discord.Discord", 4.8, "Top ranked"),
        new("Gaming", 1, "Steam", "Valve.Steam", 4.9, "Top ranked"),
        new("Utilities", 1, "Notepad++", "Notepad++.Notepad++", 4.6, "Top free")
    ];

    public IReadOnlyList<StoreCatalogEntry> LoadEmbeddedCatalog()
    {
        return SampleItems.ToList();
    }

    public Task<CatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CatalogLoadResult(LoadEmbeddedCatalog(), "fake", Array.Empty<string>()));
    }
}
