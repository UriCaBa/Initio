using Initio.Core.Models;

namespace Initio.Core.Abstractions;

public interface IBloatwareService
{
    IReadOnlyList<BloatwareDefinition> GetKnownBloatware();
    Task<HashSet<string>> DetectInstalledAsync(IReadOnlyCollection<string> packageNames, CancellationToken cancellationToken = default);
    Task<bool> RemovePackageAsync(string packageName, CancellationToken cancellationToken = default);
    Task<bool> VerifyRemovedAsync(string packageName, CancellationToken cancellationToken = default);
}
