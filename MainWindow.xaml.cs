using NewPCSetupWPF.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace NewPCSetupWPF;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string ThemePrefix = "Themes/Theme.";
    private const string DefaultProfileKey = "default";
    private const string CustomProfileKey = "custom";
    private const int InstallRetryAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(4);
    private static readonly Regex WingetIdPattern = new(@"^[A-Za-z0-9][A-Za-z0-9\.\-\+_]*$", RegexOptions.Compiled);
    private static readonly string[] TransientErrorIndicators = ["network", "temporar", "timeout", "timed out", "connection", "try again", "again later", "download", "source", "internet"];

    private static readonly IReadOnlyList<AppDefinition> DefaultCatalog =
    [
        new("Chrome", "Browsers", "Google.Chrome"),
        new("Firefox", "Browsers", "Mozilla.Firefox"),
        new("Brave", "Browsers", "Brave.Brave"),
        new("Spotify", "Media", "Spotify.Spotify"),
        new("VLC", "Media", "VideoLAN.VLC"),
        new("OBS Studio", "Media", "OBSProject.OBSStudio"),
        new("Discord", "Comms", "Discord.Discord"),
        new("Telegram", "Comms", "Telegram.TelegramDesktop"),
        new("Zoom", "Comms", "Zoom.Zoom"),
        new("Dropbox", "Cloud", "Dropbox.Dropbox"),
        new("Google Drive", "Cloud", "Google.Drive"),
        new("7-Zip", "Utilities", "7zip.7zip"),
        new("Notepad++", "Utilities", "Notepad++.Notepad++"),
        new("LibreOffice", "Docs", "TheDocumentFoundation.LibreOffice"),
        new("Adobe Acrobat Reader", "Docs", "Adobe.Acrobat.Reader.64-bit"),
        new("Steam", "Gaming", "Valve.Steam"),
        new("Epic Games Launcher", "Gaming", "EpicGames.EpicGamesLauncher"),
        new("League of Legends EUW", "Gaming", "RiotGames.LeagueOfLegends.EUW"),
        new("NVIDIA App", "Drivers", "Nvidia.NVIDIAApp"),
        new("Malwarebytes", "Security", "Malwarebytes.Malwarebytes"),
        new("VS Code", "Dev", "Microsoft.VisualStudioCode"),
        new("Git", "Dev", "Git.Git"),
        new("Docker Desktop", "Dev", "Docker.DockerDesktop")
    ];

    private readonly ObservableCollection<AppItem> _appItems = new();
    private readonly ReadOnlyCollection<ThemeOption> _themeOptions;
    private readonly ReadOnlyCollection<SelectionProfile> _selectionProfiles;
    private readonly Stopwatch _installStopwatch = new();
    private readonly HashSet<string> _knownInstalledIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _logBuilder = new();
    private readonly object _logSync = new();

    private CancellationTokenSource? _installCancellationTokenSource;
    private bool _isApplyingProfile;
    private string _searchQuery = string.Empty;
    private string _statusText = "Ready.";
    private string _etaText = "ETA: --";
    private double _progressMaximum = 1;
    private double _progressValue;
    private bool _isBusy;
    private bool _isWingetAvailable;
    private ThemeOption? _selectedTheme;
    private SelectionProfile? _selectedProfile;
    private AppItem? _selectedAppItem;
    private string _customAppName = string.Empty;
    private string _customAppCategory = string.Empty;
    private string _customAppWingetId = string.Empty;
    private string _logText = "Logs will appear here when you run an installation.";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _themeOptions = new ReadOnlyCollection<ThemeOption>(
        [
            new ThemeOption("Dark Elegant", "Themes/Theme.DarkElegant.xaml"),
            new ThemeOption("Corporate Dark", "Themes/Theme.Corporate.xaml"),
            new ThemeOption("Gamer RGB", "Themes/Theme.GamerRgb.xaml")
        ]);

        _selectionProfiles = CreateSelectionProfiles();
        _appItems.CollectionChanged += AppItems_CollectionChanged;
        AppItemsView = CollectionViewSource.GetDefaultView(_appItems);
        AppItemsView.Filter = FilterAppItem;

        ReloadDefaultCatalog();
        SelectedTheme = _themeOptions[0];
        SelectedProfile = GetProfile(DefaultProfileKey);
        SetProgress(0, 1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<AppItem> AppItems => _appItems;
    public ICollectionView AppItemsView { get; }
    public ReadOnlyCollection<ThemeOption> ThemeOptions => _themeOptions;
    public ReadOnlyCollection<SelectionProfile> SelectionProfiles => _selectionProfiles;

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetField(ref _selectedTheme, value) && value is not null)
            {
                ApplyTheme(value.ResourcePath);
            }
        }
    }

    public SelectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value) && value is not null)
            {
                ApplySelectionProfile(value);
            }
        }
    }

    public AppItem? SelectedAppItem
    {
        get => _selectedAppItem;
        set
        {
            if (SetField(ref _selectedAppItem, value))
            {
                OnPropertyChanged(nameof(CanRemoveSelectedApp));
            }
        }
    }

    public string CustomAppName
    {
        get => _customAppName;
        set => SetField(ref _customAppName, value);
    }

    public string CustomAppCategory
    {
        get => _customAppCategory;
        set => SetField(ref _customAppCategory, value);
    }

    public string CustomAppWingetId
    {
        get => _customAppWingetId;
        set => SetField(ref _customAppWingetId, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                AppItemsView.Refresh();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string EtaText
    {
        get => _etaText;
        set => SetField(ref _etaText, value);
    }

    public string LogText
    {
        get => _logText;
        set => SetField(ref _logText, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        set
        {
            var normalized = value <= 0 ? 1 : value;
            if (SetField(ref _progressMaximum, normalized))
            {
                OnPropertyChanged(nameof(ProgressSummary));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            var normalized = Math.Clamp(value, 0, ProgressMaximum);
            if (SetField(ref _progressValue, normalized))
            {
                OnPropertyChanged(nameof(ProgressSummary));
            }
        }
    }

    public string ProgressSummary
    {
        get
        {
            var max = Math.Max(1, ProgressMaximum);
            return $"{(int)ProgressValue}/{(int)max} ({(ProgressValue / max) * 100:0}%)";
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanManageSelection));
                OnPropertyChanged(nameof(CanEditCatalog));
                OnPropertyChanged(nameof(CanCancelInstall));
                OnPropertyChanged(nameof(CanRemoveSelectedApp));
            }
        }
    }

    public bool IsWingetAvailable
    {
        get => _isWingetAvailable;
        set
        {
            if (SetField(ref _isWingetAvailable, value))
            {
                OnPropertyChanged(nameof(CanInstall));
            }
        }
    }

    public bool CanInstall => IsWingetAvailable && !IsBusy;
    public bool CanManageSelection => !IsBusy;
    public bool CanEditCatalog => !IsBusy;
    public bool CanCancelInstall => IsBusy && _installCancellationTokenSource is not null;
    public bool CanRemoveSelectedApp => !IsBusy && SelectedAppItem is not null;

    public string SelectionSummary
    {
        get
        {
            var installed = _appItems.Count(x => x.IsInstalled);
            var selectable = _appItems.Count - installed;
            var selected = _appItems.Count(x => x.IsSelected && !x.IsInstalled);
            return $"{selected} selected | {selectable} available | {installed} already installed";
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await InitializeWingetAsync();
    private async void RefreshInstalled_Click(object sender, RoutedEventArgs e) => await RefreshInstalledStatesAsync();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _appItems.Where(x => !x.IsInstalled))
        {
            app.IsSelected = true;
        }
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _appItems)
        {
            app.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AddCustomApp_Click(object sender, RoutedEventArgs e)
    {
        var name = CustomAppName.Trim();
        var category = CustomAppCategory.Trim();
        var wingetId = CustomAppWingetId.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(wingetId))
        {
            StatusText = "Fill Name, Category and winget ID before adding.";
            return;
        }
        if (!WingetIdPattern.IsMatch(wingetId))
        {
            StatusText = "Invalid winget ID format.";
            return;
        }
        if (_appItems.Any(x => string.Equals(x.WingetId, wingetId, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"winget ID already exists: {wingetId}";
            return;
        }

        AddAppToCatalog(new AppDefinition(name, category, wingetId));
        SwitchToCustomProfile();
        AppItemsView.Refresh();
        AppendLog($"Custom app added: {name} ({wingetId})");
        StatusText = $"Added custom app: {name}.";
        CustomAppName = string.Empty;
        CustomAppCategory = string.Empty;
        CustomAppWingetId = string.Empty;
    }

    private void RemoveSelectedApp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAppItem is null)
        {
            StatusText = "Select an app to remove.";
            return;
        }

        var app = SelectedAppItem;
        _appItems.Remove(app);
        _knownInstalledIds.Remove(app.WingetId);
        SelectedAppItem = null;
        SwitchToCustomProfile();
        AppItemsView.Refresh();
        AppendLog($"App removed from catalog: {app.Name} ({app.WingetId})");
        StatusText = $"Removed app: {app.Name}.";
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private async void ResetDefaultCatalog_Click(object sender, RoutedEventArgs e)
    {
        ReloadDefaultCatalog();
        SelectedProfile = GetProfile(DefaultProfileKey);
        AppendLog("Default catalog restored.");
        StatusText = "Default app list restored.";
        if (IsWingetAvailable)
        {
            await RefreshInstalledStatesAsync();
        }
    }

    private async void InstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _appItems.Where(x => x.IsSelected && !x.IsInstalled).ToList();
        if (selected.Count == 0)
        {
            StatusText = "No non-installed apps are selected.";
            EtaText = "ETA: --";
            return;
        }
        await InstallAppsAsync(selected);
    }

    private async void InstallAll_Click(object sender, RoutedEventArgs e)
    {
        var installable = _appItems.Where(x => !x.IsInstalled).ToList();
        if (installable.Count == 0)
        {
            StatusText = "All listed apps are already installed.";
            EtaText = "ETA: --";
            return;
        }
        await InstallAppsAsync(installable);
    }

    private void CancelInstall_Click(object sender, RoutedEventArgs e)
    {
        _installCancellationTokenSource?.Cancel();
        StatusText = "Cancelling installation...";
        AppendLog("Cancellation requested by user.");
    }

    private async Task InitializeWingetAsync()
    {
        IsBusy = true;
        StatusText = "Checking winget availability...";
        EtaText = "ETA: --";
        SetProgress(0, 1);
        IsWingetAvailable = await CheckWingetAvailabilityAsync();

        if (!IsWingetAvailable)
        {
            StatusText = "winget was not found. Please install App Installer from Microsoft Store.";
            IsBusy = false;
            MessageBox.Show(
                this,
                "winget is not available on this machine.\nInstall 'App Installer' from Microsoft Store and restart this app.",
                "winget not found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IsBusy = false;
        await RefreshInstalledStatesAsync();
    }

    private async Task<bool> CheckWingetAvailabilityAsync()
    {
        try
        {
            var result = await RunProcessAsync("winget", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task RefreshInstalledStatesAsync()
    {
        if (!IsWingetAvailable)
        {
            StatusText = "winget is not available.";
            EtaText = "ETA: --";
            return;
        }

        IsBusy = true;
        StatusText = "Detecting installed apps...";
        EtaText = "ETA: --";
        SetProgress(0, 1);

        try
        {
            var installedIds = await GetInstalledPackageIdsAsync();
            _knownInstalledIds.Clear();
            _knownInstalledIds.UnionWith(installedIds);

            foreach (var app in _appItems)
            {
                var installed = _knownInstalledIds.Contains(app.WingetId);
                app.IsInstalled = installed;
                app.InstallStatus = installed ? "Installed" : "Ready";
            }

            SetProgress(1, 1);
            StatusText = $"Installed apps refreshed ({installedIds.Count} detected).";
            EtaText = "ETA: --";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to read installed packages: {ex.Message}";
            EtaText = "ETA: --";
        }
        finally
        {
            IsBusy = false;
            AppItemsView.Refresh();
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    private async Task<HashSet<string>> GetInstalledPackageIdsAsync()
    {
        var jsonResult = await RunProcessAsync("winget", "list --accept-source-agreements --disable-interactivity --output json");
        if (jsonResult.ExitCode == 0 && TryParseInstalledIdsFromJson(jsonResult.StandardOutput, out var parsedIds))
        {
            return parsedIds;
        }

        var textResult = await RunProcessAsync("winget", "list --accept-source-agreements --disable-interactivity");
        var combined = $"{textResult.StandardOutput}\n{textResult.StandardError}";
        return ExtractInstalledIdsFromText(combined);
    }

    private async Task InstallAppsAsync(IReadOnlyList<AppItem> targetApps)
    {
        if (!IsWingetAvailable)
        {
            StatusText = "winget is not available.";
            EtaText = "ETA: --";
            return;
        }

        _installCancellationTokenSource?.Dispose();
        _installCancellationTokenSource = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanCancelInstall));

        var cancellationToken = _installCancellationTokenSource.Token;
        var totalSteps = targetApps.Count + 1;
        var completedSteps = 0d;
        var successful = 0;
        var failed = 0;
        var skipped = 0;
        var retriesUsed = 0;

        IsBusy = true;
        SetProgress(completedSteps, totalSteps);
        StatusText = $"Preparing installation of {targetApps.Count} app(s)...";
        EtaText = "ETA: calculating...";
        ClearLogs();
        AppendLog($"Installation started for {targetApps.Count} app(s).");
        _installStopwatch.Restart();

        try
        {
            foreach (var app in targetApps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (app.IsInstalled)
                {
                    skipped++;
                    app.InstallStatus = "Installed";
                    app.IsSelected = false;
                    completedSteps += 1;
                    SetProgress(completedSteps, totalSteps);
                    UpdateEta();
                    AppendLog($"[{app.Name}] skipped (already installed).");
                    continue;
                }

                app.InstallStatus = "Installing...";
                StatusText = $"Installing {app.Name}...";
                AppendLog($"[{app.Name}] installation started.");

                var outcome = await InstallAppWithRetryAsync(app, cancellationToken);
                retriesUsed += Math.Max(0, outcome.Attempts - 1);

                if (outcome.Success)
                {
                    app.IsInstalled = true;
                    app.InstallStatus = "Installed";
                    _knownInstalledIds.Add(app.WingetId);
                    successful++;
                    AppendLog($"[{app.Name}] installed successfully.");
                }
                else
                {
                    app.InstallStatus = $"Failed (code {outcome.ExitCode})";
                    failed++;
                    AppendLog($"[{app.Name}] failed with code {outcome.ExitCode}.");
                }

                completedSteps += 1;
                SetProgress(completedSteps, totalSteps);
                UpdateEta();
                OnPropertyChanged(nameof(SelectionSummary));
            }

            cancellationToken.ThrowIfCancellationRequested();
            StatusText = "Running winget upgrade --all...";
            EtaText = "ETA: finalizing...";
            AppendLog("Running post-install upgrade: winget upgrade --all");

            var upgradeResult = await RunProcessAsync(
                "winget",
                "upgrade --all --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
                line => ReportLiveStatus($"Upgrade: {line}"),
                line => ReportLiveStatus($"Upgrade [stderr]: {line}"),
                cancellationToken);

            completedSteps += 1;
            SetProgress(completedSteps, totalSteps);
            UpdateEta();
            StatusText = upgradeResult.ExitCode == 0
                ? "Installation complete. Global upgrade finished."
                : $"Installation complete. Global upgrade exited with code {upgradeResult.ExitCode}.";
            AppendLog(upgradeResult.ExitCode == 0
                ? "Global upgrade completed successfully."
                : $"Global upgrade finished with code {upgradeResult.ExitCode}.");
            EtaText = "ETA: done";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Installation cancelled by user.";
            EtaText = "ETA: cancelled";
            AppendLog("Installation cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = $"Installation interrupted: {ex.Message}";
            EtaText = "ETA: --";
            AppendLog($"Installation interrupted: {ex.Message}");
        }
        finally
        {
            _installStopwatch.Stop();
            AppendLog($"Summary => success: {successful}, failed: {failed}, skipped: {skipped}, retries: {retriesUsed}");
            IsBusy = false;
            _installCancellationTokenSource?.Dispose();
            _installCancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancelInstall));
            AppItemsView.Refresh();
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    private async Task<InstallOutcome> InstallAppWithRetryAsync(AppItem app, CancellationToken cancellationToken)
    {
        ProcessResult? lastResult = null;

        for (var attempt = 1; attempt <= InstallRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installArgs = $"install --exact --id {app.WingetId} --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
            AppendLog($"[{app.Name}] attempt {attempt}/{InstallRetryAttempts}");

            lastResult = await RunProcessAsync(
                "winget",
                installArgs,
                line => ReportLiveStatus($"{app.Name}: {line}"),
                line => ReportLiveStatus($"{app.Name} [stderr]: {line}"),
                cancellationToken);

            if (lastResult.ExitCode == 0)
            {
                return new InstallOutcome(true, lastResult.ExitCode, attempt);
            }

            var canRetry = attempt < InstallRetryAttempts && IsTransientWingetFailure(lastResult);
            if (!canRetry)
            {
                break;
            }

            app.InstallStatus = $"Retrying ({attempt}/{InstallRetryAttempts})...";
            StatusText = $"{app.Name} failed transiently. Retrying...";
            AppendLog($"[{app.Name}] transient failure detected. Retrying in {RetryDelay.TotalSeconds:0}s.");
            await Task.Delay(RetryDelay, cancellationToken);
        }

        return new InstallOutcome(false, lastResult?.ExitCode ?? -1, InstallRetryAttempts);
    }

    private static bool IsTransientWingetFailure(ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            return false;
        }

        var lower = $"{result.StandardOutput}\n{result.StandardError}".ToLowerInvariant();
        return TransientErrorIndicators.Any(indicator => lower.Contains(indicator, StringComparison.Ordinal));
    }

    private void ReportLiveStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            StatusText = message.Trim();
            AppendLogCore($"[{DateTime.Now:HH:mm:ss}] {message.Trim()}");
            UpdateEta();
            return;
        }

        Dispatcher.Invoke(() =>
        {
            StatusText = message.Trim();
            AppendLogCore($"[{DateTime.Now:HH:mm:ss}] {message.Trim()}");
            UpdateEta();
        });
    }

    private void SetProgress(double value, double maximum)
    {
        ProgressMaximum = maximum <= 0 ? 1 : maximum;
        ProgressValue = value;
    }

    private void UpdateEta()
    {
        if (!_installStopwatch.IsRunning)
        {
            return;
        }

        if (ProgressValue <= 0)
        {
            EtaText = "ETA: calculating...";
            return;
        }

        var remainingSteps = ProgressMaximum - ProgressValue;
        if (remainingSteps <= 0)
        {
            EtaText = "ETA: done";
            return;
        }

        var averageSecondsPerStep = _installStopwatch.Elapsed.TotalSeconds / ProgressValue;
        var eta = TimeSpan.FromSeconds(averageSecondsPerStep * remainingSteps);
        EtaText = $"ETA: {eta:hh\\:mm\\:ss}";
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, Action<string>? onOutput = null, Action<string>? onError = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null) return;
            standardOutput.AppendLine(eventArgs.Data);
            onOutput?.Invoke(eventArgs.Data);
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null) return;
            standardError.AppendLine(eventArgs.Data);
            onError?.Invoke(eventArgs.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start process: {fileName}");
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static bool TryParseInstalledIdsFromJson(string json, out HashSet<string> ids)
    {
        ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            CollectPackageIds(document.RootElement, ids);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CollectPackageIds(JsonElement element, HashSet<string> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray()) CollectPackageIds(child, ids);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("PackageIdentifier") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var id = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            ids.Add(id.Trim());
                        }
                    }
                    CollectPackageIds(property.Value, ids);
                }
                break;
        }
    }

    private HashSet<string> ExtractInstalledIdsFromText(string fullOutput)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in _appItems)
        {
            if (ContainsPackageId(fullOutput, app.WingetId))
            {
                ids.Add(app.WingetId);
            }
        }
        return ids;
    }

    private static bool ContainsPackageId(string fullOutput, string wingetId)
    {
        if (string.IsNullOrWhiteSpace(fullOutput))
        {
            return false;
        }
        return Regex.IsMatch(fullOutput, $@"(?im)(?<!\S){Regex.Escape(wingetId)}(?!\S)", RegexOptions.CultureInvariant);
    }

    private bool FilterAppItem(object item)
    {
        if (item is not AppItem app)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        var term = SearchQuery.Trim();
        return app.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.WingetId.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.InstallStatus.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private void AppItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (AppItem app in e.NewItems) app.PropertyChanged += AppItem_PropertyChanged;
        }
        if (e.OldItems is not null)
        {
            foreach (AppItem app in e.OldItems) app.PropertyChanged -= AppItem_PropertyChanged;
        }
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AppItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppItem.IsSelected) or nameof(AppItem.IsInstalled))
        {
            OnPropertyChanged(nameof(SelectionSummary));
        }

        if (e.PropertyName == nameof(AppItem.IsSelected) && !IsBusy && !_isApplyingProfile && SelectedProfile is not null && !SelectedProfile.IsCustom)
        {
            SwitchToCustomProfile();
        }
    }

    private void ApplyTheme(string resourcePath)
    {
        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
        var currentTheme = mergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source is not null &&
            dictionary.Source.OriginalString.StartsWith(ThemePrefix, StringComparison.OrdinalIgnoreCase));

        if (currentTheme is not null)
        {
            mergedDictionaries.Remove(currentTheme);
        }

        mergedDictionaries.Add(new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Relative) });
    }

    private void ApplySelectionProfile(SelectionProfile profile)
    {
        if (profile.IsCustom)
        {
            return;
        }

        _isApplyingProfile = true;
        try
        {
            foreach (var app in _appItems)
            {
                app.IsSelected = !app.IsInstalled && profile.SelectedWingetIds.Contains(app.WingetId);
            }
        }
        finally
        {
            _isApplyingProfile = false;
        }
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void ReloadDefaultCatalog()
    {
        _appItems.Clear();
        foreach (var app in DefaultCatalog) AddAppToCatalog(app);
        AppItemsView.Refresh();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AddAppToCatalog(AppDefinition definition)
    {
        if (_appItems.Any(x => string.Equals(x.WingetId, definition.WingetId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var app = new AppItem { Name = definition.Name, Category = definition.Category, WingetId = definition.WingetId };
        if (_knownInstalledIds.Contains(definition.WingetId))
        {
            app.IsInstalled = true;
            app.InstallStatus = "Installed";
        }
        _appItems.Add(app);
    }

    private void SwitchToCustomProfile()
    {
        var customProfile = GetProfile(CustomProfileKey);
        if (SelectedProfile?.Key != customProfile.Key)
        {
            SelectedProfile = customProfile;
        }
    }

    private SelectionProfile GetProfile(string key) => _selectionProfiles.First(profile => profile.Key == key);

    private ReadOnlyCollection<SelectionProfile> CreateSelectionProfiles()
    {
        var defaultIds = new HashSet<string>(DefaultCatalog.Select(x => x.WingetId), StringComparer.OrdinalIgnoreCase);
        var essentials = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google.Chrome", "VideoLAN.VLC", "Spotify.Spotify", "Discord.Discord", "Zoom.Zoom",
            "7zip.7zip", "Notepad++.Notepad++", "TheDocumentFoundation.LibreOffice", "Adobe.Acrobat.Reader.64-bit", "Nvidia.NVIDIAApp"
        };
        var devRig = new HashSet<string>(essentials, StringComparer.OrdinalIgnoreCase)
        {
            "Mozilla.Firefox", "Microsoft.VisualStudioCode", "Git.Git", "Docker.DockerDesktop", "OBSProject.OBSStudio"
        };

        return new ReadOnlyCollection<SelectionProfile>(
        [
            new SelectionProfile(DefaultProfileKey, "Default (Requested)", defaultIds),
            new SelectionProfile("recommended", "Recommended Essentials", essentials),
            new SelectionProfile("dev", "Recommended Dev Rig", devRig),
            new SelectionProfile(CustomProfileKey, "Custom", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true)
        ]);
    }

    private void ClearLogs()
    {
        if (Dispatcher.CheckAccess())
        {
            ClearLogsCore();
            return;
        }
        Dispatcher.Invoke(ClearLogsCore);
    }

    private void ClearLogsCore()
    {
        lock (_logSync)
        {
            _logBuilder.Clear();
            LogText = string.Empty;
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
        var line = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        if (Dispatcher.CheckAccess())
        {
            AppendLogCore(line);
            return;
        }
        Dispatcher.Invoke(() => AppendLogCore(line));
    }

    private void AppendLogCore(string line)
    {
        lock (_logSync)
        {
            _logBuilder.AppendLine(line);
            LogText = _logBuilder.ToString();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public sealed record SelectionProfile(string Key, string DisplayName, HashSet<string> SelectedWingetIds, bool IsCustom = false);
    public sealed record ThemeOption(string DisplayName, string ResourcePath);
    private sealed record AppDefinition(string Name, string Category, string WingetId);
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    private sealed record InstallOutcome(bool Success, int ExitCode, int Attempts);
}
