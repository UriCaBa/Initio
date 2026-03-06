using Initio.Core.Infrastructure;
using Initio.Core.Models;
using Initio.Core.Services;

namespace Initio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private async Task RefreshInstalledStatesAsync()
    {
        if (!IsWingetAvailable)
        {
            StatusText = "winget is not available.";
            return;
        }

        var output = await _wingetClient.ListInstalledAsync();
        if (output is null)
        {
            return;
        }

        _installedInventoryOutput = output;
        Catalog.RefreshInstalledStates(output, _knownInstalledIds);
        UpdateCatalogStatuses();
        StatusText = "Installed states refreshed.";
        AppendLog("Refreshed installed app states.");
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private async Task InstallSelectedAsync()
    {
        var targets = Catalog.GetPendingSelectedItems();
        if (targets.Count == 0)
        {
            StatusText = "No apps selected for install.";
            return;
        }

        IsBusy = true;
        _installCancellationTokenSource = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanCancelInstall));
        RefreshCommandStates();

        try
        {
            await ExecuteBatchOperationAsync(
                targets,
                "Installation",
                "installation",
                "Installing",
                "installed",
                "installation",
                app => app.Name,
                app => app.WingetId,
                app => app.InstallStatus = "Installing...",
                InstallSingleAppAsync,
                app =>
                {
                    app.IsInstalled = true;
                    app.InstallStatus = "Installed";
                    _knownInstalledIds.Add(app.WingetId);
                },
                app => app.InstallStatus = "Failed",
                UpdateCatalogStatuses,
                _installCancellationTokenSource.Token);
        }
        finally
        {
            IsBusy = false;
            _installCancellationTokenSource?.Dispose();
            _installCancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancelInstall));
            RefreshCommandStates();
        }
    }

    private void CancelInstall()
    {
        _installCancellationTokenSource?.Cancel();
        AppendLog("Installation cancelled by user.");
        StatusText = "Installation cancelled.";
    }

    private async Task<bool> InstallSingleAppAsync(AppItem app, CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidWingetId(app.WingetId))
        {
            AppendLog($"  Skipping {app.Name}: invalid WingetId '{app.WingetId}'.");
            return false;
        }

        return await RetryHelper.ExecuteAsync(
            MaxInstallRetries,
            async (_, token) =>
            {
                var result = await _wingetClient.InstallAsync(app.WingetId, IsSilentInstall, SingleAppTimeout, token);
                if (result is not null &&
                    (result.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase) ||
                     result.Contains("Already installed", StringComparison.OrdinalIgnoreCase) ||
                     result.Contains("No available upgrade", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (await VerifyInstalledAsync(app.WingetId, token))
                {
                    return true;
                }

                return await VerifyInstalledAsync(app.Name, token);
            },
            attempt => AppendLog($"  Retry ({attempt}/{MaxInstallRetries}) for {app.Name}..."),
            cancellationToken);
    }

    private async Task<bool> VerifyInstalledAsync(string query, CancellationToken cancellationToken)
    {
        var output = await _wingetClient.ListInstalledAsync(query, cancellationToken);
        return output is not null && output.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SearchWingetAsync()
    {
        if (IsBusy || Search.IsSearching || !IsWingetAvailable)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Search.Query))
        {
            AppendLog("Enter a search query first.");
            return;
        }

        Search.IsSearching = true;
        RefreshCommandStates();
        AppendLog($"Searching winget for '{Search.Query}'...");

        try
        {
            var results = await _wingetClient.SearchAsync(Search.Query, 60);
            Search.SetResults(results);
            Search.UpdateCatalogStatus(Catalog.AllItems, _installedInventoryOutput);
            OnPropertyChanged(nameof(StoreSelectionSummary));
            AppendLog($"Search completed with {results.Count} result(s).");
        }
        catch (Exception ex)
        {
            AppendLog($"Search failed: {ex.Message}");
        }
        finally
        {
            Search.IsSearching = false;
            RefreshCommandStates();
        }
    }

    private void AddSelectedStoreApps()
    {
        var selectedItems = Store.AllItems.Where(item => item.IsSelected)
            .Concat(Search.Results.Where(item => item.IsSelected))
            .DistinctBy(item => item.WingetId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedItems.Count == 0)
        {
            AppendLog("Check one or more apps to add.");
            return;
        }

        var added = 0;
        var skipped = 0;
        foreach (var item in selectedItems)
        {
            if (Catalog.AddApp(new AppDefinition(item.Name, item.Category, item.WingetId), _knownInstalledIds))
            {
                added++;
                AppendLog($"App added: {item.Name} ({item.WingetId})");
            }
            else
            {
                skipped++;
            }

            item.IsSelected = false;
        }

        UpdateCatalogStatuses();
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(StoreSelectionSummary));
        AppendLog(added > 0
            ? $"Added {added} app(s)." + (skipped > 0 ? $" ({skipped} already in catalog)" : string.Empty)
            : $"All {skipped} app(s) already in catalog.");
    }

    private void RemoveSelectedCatalogApp()
    {
        var removed = Catalog.RemoveSelected();
        if (removed is null)
        {
            AppendLog("Select an app to remove.");
            return;
        }

        _knownInstalledIds.Remove(removed.WingetId);
        UpdateCatalogStatuses();
        AppendLog($"Removed: {removed.Name} ({removed.WingetId})");
        OnPropertyChanged(nameof(SelectionSummary));
        RefreshCommandStates();
    }

    private async Task ResetDefaultCatalogAsync()
    {
        Catalog.LoadDefaults(MainViewModelPresets.DefaultCatalog, _knownInstalledIds);
        SelectedProfile = SelectionProfiles.First(profile => profile.Key == DefaultProfileKey);
        UpdateCatalogStatuses();
        AppendLog("Default catalog restored.");
        if (IsWingetAvailable)
        {
            await RefreshInstalledStatesAsync();
        }
    }
}
