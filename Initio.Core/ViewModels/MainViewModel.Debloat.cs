using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private async Task ScanBloatwareAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Scanning for bloatware...";
        try
        {
            await DetectBloatwareAsync();
            StatusText = "Bloatware scan complete.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DetectBloatwareAsync()
    {
        AppendLog("Scanning for bloatware packages...");
        try
        {
            var packageNames = Debloat.AllItems.Select(item => item.PackageName).ToArray();
            var installed = await _bloatwareService.DetectInstalledAsync(packageNames);
            Debloat.ApplyDetectedPackages(installed);
            AppendLog($"Scan complete: {installed.Count} bloatware package(s) detected.");
            OnPropertyChanged(nameof(BloatwareSummary));
        }
        catch (Exception ex)
        {
            AppendLog($"Bloatware scan error: {ex.Message}");
        }
    }

    private async Task RemoveBloatwareAsync()
    {
        var targets = Debloat.GetSelectedInstalledItems();
        if (targets.Count == 0)
        {
            StatusText = "No bloatware selected for removal.";
            return;
        }

        IsBusy = true;
        _debloatCancellationTokenSource = new CancellationTokenSource();

        try
        {
            await ExecuteBatchOperationAsync(
                targets,
                "Removal",
                "removal",
                "Removing",
                "removed",
                "removal",
                item => item.Name,
                item => item.PackageName,
                item => item.RemovalStatus = "Removing...",
                RemoveSinglePackageAsync,
                item =>
                {
                    item.IsInstalled = false;
                    item.RemovalStatus = "Removed";
                },
                item => item.RemovalStatus = "Failed",
                () => OnPropertyChanged(nameof(BloatwareSummary)),
                _debloatCancellationTokenSource.Token);
        }
        finally
        {
            IsBusy = false;
            _debloatCancellationTokenSource?.Dispose();
            _debloatCancellationTokenSource = null;
            Debloat.RefreshVisibleItems();
            RefreshCommandStates();
        }
    }

    private void CancelDebloat()
    {
        _debloatCancellationTokenSource?.Cancel();
        AppendLog("Bloatware removal cancelled by user.");
        StatusText = "Removal cancelled.";
    }

    private async Task<bool> RemoveSinglePackageAsync(BloatwareItem item, CancellationToken cancellationToken)
    {
        return await RetryHelper.ExecuteAsync(
            MaxInstallRetries,
            async (_, token) =>
            {
                var removed = await _bloatwareService.RemovePackageAsync(item.PackageName, token);
                return removed && await _bloatwareService.VerifyRemovedAsync(item.PackageName, token);
            },
            attempt => AppendLog($"  Retry ({attempt}/{MaxInstallRetries}) for {item.Name}..."),
            cancellationToken).ConfigureAwait(false);
    }
}
