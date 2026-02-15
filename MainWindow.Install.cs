// MainWindow.Install.cs
// Partial class containing all winget-related logic: installation, refresh,
// cancellation, window load, and progress tracking.
// This is separated from the main code-behind to keep file sizes manageable.

using System.Diagnostics;
using System.Windows;

namespace NewPCSetupWPF;

public partial class MainWindow
{
    internal CancellationTokenSource? _installCancellationTokenSource;
    private static readonly TimeSpan SingleAppTimeout = TimeSpan.FromMinutes(15);
    private const int MaxInstallRetries = 2;
    private const string WingetAcceptFlags = "--accept-package-agreements --accept-source-agreements";

    // ═══ Window Load ═══
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AppendLog("Checking for winget availability...");
        StatusText = "Initializing...";
        await InitializeWingetAsync();
    }

    // ═══ Winget Initialization ═══
    private async Task InitializeWingetAsync()
    {
        try
        {
            var available = await RunWingetCommandAsync("--version");
            IsWingetAvailable = available is not null;
            if (IsWingetAvailable)
            {
                AppendLog($"winget detected: {available?.Trim()}");
                StatusText = "winget available — ready to install.";
                await RefreshInstalledStatesAsync();
                _ = DetectBloatwareAsync();
            }
            else
            {
                StatusText = "winget NOT found — install App Installer from the Microsoft Store.";
                AppendLog("ERROR: winget not detected.");
            }
        }
        catch (Exception ex)
        {
            IsWingetAvailable = false;
            StatusText = $"Error checking winget: {ex.Message}";
            AppendLog($"Init error: {ex.Message}");
        }
    }

    // ═══ Refresh Installed States ═══
    private async void RefreshInstalled_Click(object sender, RoutedEventArgs e)
    {
        if (!IsWingetAvailable) { StatusText = "winget is not available."; return; }
        StatusText = "Refreshing installed states...";
        IsBusy = true;
        try
        {
            await RefreshInstalledStatesAsync();
            StatusText = "Installed states refreshed.";
            AppendLog("Refreshed installed app states.");
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
            AppendLog($"Refresh error: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    internal async Task RefreshInstalledStatesAsync()
    {
        var output = await RunWingetCommandAsync($"list {WingetAcceptFlags}");
        if (output is null) return;

        _knownInstalledIds.Clear();
        foreach (var app in _appItems)
        {
            // Check by ID first (most accurate)
            var isInstalled = output.Contains(app.WingetId, StringComparison.OrdinalIgnoreCase);
            
            // Fallback: Check by Name if ID fails (fixes Store vs WinGet mismatches like Spotify)
            if (!isInstalled && !string.IsNullOrWhiteSpace(app.Name))
            {
                // Simple containment check. 
                // formatted output usually has headers, so this avoids matching "Name" header if app name was "Name" (unlikely)
                isInstalled = output.Contains(app.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (isInstalled) _knownInstalledIds.Add(app.WingetId);
            app.IsInstalled = isInstalled;
            app.InstallStatus = isInstalled ? "Installed" : "Pending";
        }

        
        // Update store catalog status using the full winget output
        // This ensures items in Store tabs show as "Installed" even if not in "My Setup"
        UpdateStoreCatalogStatus(output);
        OnPropertyChanged(nameof(SelectionSummary));
    }

    // ═══ Install Selected / Install All ═══
    private async void InstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var targets = _appItems.Where(x => x.IsSelected && !x.IsInstalled).ToList();
        if (targets.Count == 0) { StatusText = "No apps selected for install."; return; }
        await InstallAppsAsync(targets);
    }

    private async void InstallAll_Click(object sender, RoutedEventArgs e)
    {
        var targets = _appItems.Where(x => !x.IsInstalled).ToList();
        if (targets.Count == 0) { StatusText = "All apps already installed."; return; }
        await InstallAppsAsync(targets);
    }

    private void CancelInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_installCancellationTokenSource is null) return;
        _installCancellationTokenSource.Cancel();
        CancelActiveWingetProcess();
        AppendLog("Installation cancelled by user.");
        StatusText = "Installation cancelled.";
    }

    // ═══ Main Installation Loop ═══
    private async Task InstallAppsAsync(IReadOnlyList<Models.AppItem> targetApps)
    {
        IsBusy = true;
        _hasInstallSession = true;
        _installCancellationTokenSource = new CancellationTokenSource();
        var ct = _installCancellationTokenSource.Token;
        var total = targetApps.Count;
        SetProgress(0, total);
        EtaText = $"ETA: calculating...";
        ClearLogs();
        AppendLog($"Starting installation of {total} app(s)…");

        var stopwatch = Stopwatch.StartNew();
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < total; index++)
            {
                ct.ThrowIfCancellationRequested();

                var app = targetApps[index];
                var position = index + 1;
                app.InstallStatus = "Installing...";
                StatusText = $"[{position}/{total}] Installing {app.Name}…";
                AppendLog($"[{position}/{total}] Installing {app.Name} ({app.WingetId})...");

                var installed = await InstallSingleAppAsync(app, ct);
                if (installed)
                {
                    succeeded++;
                    app.IsInstalled = true;
                    app.InstallStatus = "Installed";
                    _knownInstalledIds.Add(app.WingetId);
                    AppendLog($"  ✓ {app.Name} installed successfully.");
                }
                else
                {
                    failed++;
                    app.InstallStatus = "Failed";
                    AppendLog($"  ✗ {app.Name} installation failed.");
                }

                ProgressValue = position;

                UpdateEta(stopwatch, position, total);
            }

            StatusText = $"Done — {succeeded} installed, {failed} failed.";
            AppendLog($"Installation complete: {succeeded} succeeded, {failed} failed ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Installation was cancelled.";
            AppendLog($"Cancelled after {succeeded} installs ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"Unexpected error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _installCancellationTokenSource.Dispose();
            _installCancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancelInstall));
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    /// <summary>Install a single app with retry support.</summary>
    private async Task<bool> InstallSingleAppAsync(Models.AppItem app, CancellationToken ct)
    {


        for (int attempt = 1; attempt <= MaxInstallRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (attempt > 1) AppendLog($"  Retry ({attempt}/{MaxInstallRetries}) for {app.Name}…");

            if (!Services.InputValidation.IsValidWingetId(app.WingetId))
            {
                AppendLog($"  Skipping {app.Name}: invalid WingetId '{app.WingetId}'.");
                return false;
            }

            var silentFlag = IsSilentInstall ? "--silent" : "";
            var args = $"install --id \"{app.WingetId}\" {silentFlag} {WingetAcceptFlags}";
            
            var result = await RunWingetCommandAsync(args, SingleAppTimeout, ct);

            if (result is not null &&
                (result.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase) ||
                 result.Contains("Already installed", StringComparison.OrdinalIgnoreCase) ||
                 result.Contains("No available upgrade", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Fallback 1: Verify by ID (exact match)
            if (await VerifyAppInstalledAsync(app.WingetId, ct)) return true;

            // Fallback 2: Verify by Name (fuzzy match, handles Store vs WinGet ID mismatches like Spotify)
            // e.g. "Spotify" vs "Spotify.Spotify"
            if (await VerifyAppInstalledAsync(app.Name, ct)) return true;
        }
        return false;
    }

    private async Task<bool> VerifyAppInstalledAsync(string query, CancellationToken ct)
    {
        // 'winget list <query>' works for both ID and Name
        var output = await RunWingetCommandAsync($"list \"{query}\" {WingetAcceptFlags}", TimeSpan.FromSeconds(15), ct);
        return output != null && output.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    // ═══ Process Management ═══
    private Process? _currentWingetProcess;

    /// <summary>Runs an arbitrary winget command and captures its output.</summary>
    private async Task<string?> RunWingetCommandAsync(
        string arguments,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            _currentWingetProcess = process;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(effectiveTimeout);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout, kill the process
                TryKillProcess(process);
                throw new TimeoutException($"winget command timed out after {effectiveTimeout.TotalSeconds}s.");
            }

            var output = await outputTask;
            var error = await errorTask;
            return !string.IsNullOrWhiteSpace(output) ? output : error;
        }
        catch (TimeoutException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
        finally { _currentWingetProcess = null; }
    }

    private void CancelActiveWingetProcess() => TryKillProcess(_currentWingetProcess);

    private static void TryKillProcess(Process? process)
    {
        if (process is null || process.HasExited) return;
        try { process.Kill(entireProcessTree: true); }
        catch { /* process already exited */ }
    }
}
