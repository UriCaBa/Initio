using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace NewPCSetupWPF.Services;

public sealed class FakeBloatwareService : IBloatwareService
{
    private static readonly IReadOnlyList<BloatwareDefinition> SampleItems =
    [
        new("Candy Crush Saga", "Games", "king.com.CandyCrushSaga", "Pre-installed game"),
        new("TikTok", "Social & Entertainment", "BytedancePte.Ltd.TikTok", "Pre-installed social app"),
        new("Xbox Game Bar", "Promotions", "Microsoft.XboxGamingOverlay", "Gaming overlay")
    ];

    public IReadOnlyList<BloatwareDefinition> GetKnownBloatware() => SampleItems;

    public Task<HashSet<string>> DetectInstalledAsync(IReadOnlyCollection<string> packageNames, CancellationToken cancellationToken = default)
    {
        var installed = packageNames
            .Where(packageName => !string.Equals(packageName, "Microsoft.XboxGamingOverlay", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(installed);
    }

    public Task<bool> RemovePackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> VerifyRemovedAsync(string packageName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
