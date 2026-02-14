using System.Diagnostics;
using System.Windows;
using NewPCSetupWPF.Models;
using NewPCSetupWPF.Services;

namespace NewPCSetupWPF;

public partial class MainWindow
{
    private CancellationTokenSource? _debloatCancellationTokenSource;
    private const int MaxRemovalRetries = 2;

    // ═══ Debloat Detection ═══

    private async Task DetectBloatwareAsync()
    {
        AppendLog("Scanning for bloatware packages...");
        try
        {
            var installed = await BloatwareService.DetectInstalledAsync(_bloatwareItems.ToList());
            await Dispatcher.InvokeAsync(() =>
            {
                BloatwareItemsView.Refresh();
                OnPropertyChanged(nameof(BloatwareSummary));
                AppendLog($"Scan complete: {installed.Count} bloatware package(s) detected.");
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Bloatware scan error: {ex.Message}");
        }
    }

    private async void ScanBloatware_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Scanning for bloatware...";
        try
        {
            await DetectBloatwareAsync();
            StatusText = "Bloatware scan complete.";
        }
        finally { IsBusy = false; }
    }

    // ═══ Debloat Selection Handlers ═══

    private void DebloatSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (BloatwareItem item in BloatwareItemsView)
        {
            if (item.IsInstalled)
                item.IsSelected = true;
        }
    }

    private void DebloatSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _bloatwareItems)
            item.IsSelected = false;
    }

    // ═══ Remove Selected Bloatware ═══

    private async void RemoveBloatware_Click(object sender, RoutedEventArgs e)
    {
        var targets = _bloatwareItems.Where(x => x.IsSelected && x.IsInstalled).ToList();
        if (targets.Count == 0)
        {
            StatusText = "No bloatware selected for removal.";
            return;
        }
        await RemoveBloatwareAsync(targets);
    }

    private void CancelDebloat_Click(object sender, RoutedEventArgs e)
    {
        if (_debloatCancellationTokenSource is null) return;
        _debloatCancellationTokenSource.Cancel();
        AppendLog("Bloatware removal cancelled by user.");
        StatusText = "Removal cancelled.";
    }

    private async Task RemoveBloatwareAsync(IReadOnlyList<BloatwareItem> targets)
    {
        IsBusy = true;
        _hasInstallSession = true;
        _debloatCancellationTokenSource = new CancellationTokenSource();
        var ct = _debloatCancellationTokenSource.Token;
        var total = targets.Count;
        SetProgress(0, total);
        EtaText = "ETA: calculating...";
        ClearLogs();
        AppendLog($"Starting removal of {total} bloatware package(s)...");

        var stopwatch = Stopwatch.StartNew();
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < total; index++)
            {
                ct.ThrowIfCancellationRequested();

                var item = targets[index];
                var position = index + 1;
                item.RemovalStatus = "Removing...";
                StatusText = $"[{position}/{total}] Removing {item.Name}...";
                AppendLog($"[{position}/{total}] Removing {item.Name} ({item.PackageName})...");

                var removed = await RemoveSinglePackageAsync(item, ct);
                if (removed)
                {
                    succeeded++;
                    item.IsInstalled = false;
                    item.RemovalStatus = "Removed";
                    AppendLog($"  OK {item.Name} removed successfully.");
                }
                else
                {
                    failed++;
                    item.RemovalStatus = "Failed";
                    AppendLog($"  FAIL {item.Name} removal failed.");
                }

                ProgressValue = position;

                if (position < total)
                {
                    var elapsed = stopwatch.Elapsed;
                    var avgPerItem = elapsed / position;
                    var remaining = avgPerItem * (total - position);
                    EtaText = $"ETA: {remaining:mm\\:ss} remaining";
                }
                else
                {
                    EtaText = $"Completed in {stopwatch.Elapsed:mm\\:ss}";
                }
            }

            StatusText = $"Done — {succeeded} removed, {failed} failed.";
            AppendLog($"Removal complete: {succeeded} succeeded, {failed} failed ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Removal was cancelled.";
            AppendLog($"Cancelled after {succeeded} removals ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"Unexpected error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _debloatCancellationTokenSource.Dispose();
            _debloatCancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancelDebloat));
            OnPropertyChanged(nameof(BloatwareSummary));
            BloatwareItemsView.Refresh();
        }
    }

    private async Task<bool> RemoveSinglePackageAsync(BloatwareItem item, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRemovalRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (attempt > 1) AppendLog($"  Retry ({attempt}/{MaxRemovalRetries}) for {item.Name}...");

            var result = await BloatwareService.RemovePackageAsync(item.PackageName, ct);

            if (result)
            {
                if (await BloatwareService.VerifyRemovedAsync(item.PackageName, ct))
                    return true;
            }
        }
        return false;
    }
}
