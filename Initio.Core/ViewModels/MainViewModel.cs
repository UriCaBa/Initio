using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Initio.Core.Abstractions;
using Initio.Core.Infrastructure;
using Initio.Core.Models;

namespace Initio.Core.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string DefaultProfileKey = "default";
    private const string CustomProfileKey = "custom";
    private static readonly TimeSpan SingleAppTimeout = TimeSpan.FromMinutes(15);
    private const int MaxInstallRetries = 2;

    private readonly ICatalogService _catalogService;
    private readonly IWingetClient _wingetClient;
    private readonly IBloatwareService _bloatwareService;
    private readonly HashSet<string> _knownInstalledIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _logBuilder = new();
    private string? _installedInventoryOutput;
    private CancellationTokenSource? _installCancellationTokenSource;
    private CancellationTokenSource? _debloatCancellationTokenSource;
    private string _statusText = "Ready.";
    private string _etaText = "ETA: --";
    private string _logText = "Logs will appear here when you run an installation.";
    private double _progressMaximum = 1;
    private double _progressValue;
    private bool _isBusy;
    private bool _isWingetAvailable;
    private bool _isSilentInstall;
    private bool _isCompactLayout;
    private ThemeOption? _selectedTheme;
    private SelectionProfile? _selectedProfile;

    public MainViewModel(ICatalogService catalogService, IWingetClient wingetClient, IBloatwareService bloatwareService)
    {
        _catalogService = catalogService;
        _wingetClient = wingetClient;
        _bloatwareService = bloatwareService;

        Catalog = new CatalogViewModel();
        Store = new StoreViewModel();
        Search = new SearchViewModel();
        Debloat = new DebloatViewModel();

        ThemeOptions = new ReadOnlyCollection<ThemeOption>(
        [
            new ThemeOption("Midnight Blue", "/Themes/Theme.DarkElegant.xaml"),
            new ThemeOption("Neon Cyberpunk", "/Themes/Theme.GamerRgb.xaml"),
            new ThemeOption("Slate Professional", "/Themes/Theme.Corporate.xaml"),
            new ThemeOption("Gemini AI", "/Themes/Theme.Gemini.xaml"),
            new ThemeOption("Hacker Terminal", "/Themes/Theme.Hacker.xaml")
        ]);
        SelectionProfiles = new ReadOnlyCollection<SelectionProfile>(MainViewModelPresets.CreateSelectionProfiles(DefaultProfileKey, CustomProfileKey));

        foreach (var profile in SelectionProfiles)
        {
            profile.PropertyChanged += ChildStateChanged;
        }

        Catalog.PropertyChanged += ChildStateChanged;
        Store.PropertyChanged += ChildStateChanged;
        Search.PropertyChanged += ChildStateChanged;
        Debloat.PropertyChanged += ChildStateChanged;

        SelectProfileCommand = new RelayCommand(SelectProfile);
        SelectStoreCategoryCommand = new RelayCommand(SelectStoreCategory);
        SelectBloatwareCategoryCommand = new RelayCommand(SelectBloatwareCategory);
        StoreSelectAllCommand = new RelayCommand(_ => Store.SelectAllVisible(), _ => CanManageSelection);
        StoreSelectNoneCommand = new RelayCommand(_ => Store.SelectNone(), _ => CanManageSelection);
        SearchSelectAllCommand = new RelayCommand(_ => Search.SelectAll(), _ => CanManageSelection && !Search.IsSearching);
        SearchSelectNoneCommand = new RelayCommand(_ => Search.SelectNone(), _ => CanManageSelection && !Search.IsSearching);
        DebloatSelectAllCommand = new RelayCommand(_ => Debloat.SelectAllVisibleInstalled(), _ => CanDebloat);
        DebloatSelectNoneCommand = new RelayCommand(_ => Debloat.SelectNone(), _ => CanDebloat);
        AddStoreTrendAppCommand = new RelayCommand(_ => AddSelectedStoreApps(), _ => !IsBusy);
        RemoveSelectedAppCommand = new RelayCommand(_ => RemoveSelectedCatalogApp(), _ => CanRemoveSelectedApp);
        SearchCommand = new AsyncRelayCommand(_ => SearchWingetAsync(), _ => !Search.IsSearching);
        ResetDefaultCatalogCommand = new AsyncRelayCommand(_ => ResetDefaultCatalogAsync(), _ => CanEditCatalog);
        RefreshInstalledCommand = new AsyncRelayCommand(_ => RefreshInstalledStatesAsync(), _ => CanInstall);
        InstallSelectedCommand = new AsyncRelayCommand(_ => InstallSelectedAsync(), _ => CanInstall);
        CancelInstallCommand = new RelayCommand(_ => CancelInstall(), _ => CanCancelInstall);
        ScanBloatwareCommand = new AsyncRelayCommand(_ => ScanBloatwareAsync(), _ => CanDebloat);
        RemoveBloatwareCommand = new AsyncRelayCommand(_ => RemoveBloatwareAsync(), _ => CanDebloat);
        CancelDebloatCommand = new RelayCommand(_ => CancelDebloat(), _ => CanCancelDebloat);

        SelectedTheme = ThemeOptions[0];
    }

    public CatalogViewModel Catalog { get; }
    public StoreViewModel Store { get; }
    public SearchViewModel Search { get; }
    public DebloatViewModel Debloat { get; }
    public ReadOnlyCollection<ThemeOption> ThemeOptions { get; }
    public ReadOnlyCollection<SelectionProfile> SelectionProfiles { get; }

    public RelayCommand SelectProfileCommand { get; }
    public RelayCommand SelectStoreCategoryCommand { get; }
    public RelayCommand SelectBloatwareCategoryCommand { get; }
    public RelayCommand StoreSelectAllCommand { get; }
    public RelayCommand StoreSelectNoneCommand { get; }
    public RelayCommand SearchSelectAllCommand { get; }
    public RelayCommand SearchSelectNoneCommand { get; }
    public RelayCommand DebloatSelectAllCommand { get; }
    public RelayCommand DebloatSelectNoneCommand { get; }
    public RelayCommand AddStoreTrendAppCommand { get; }
    public RelayCommand RemoveSelectedAppCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand ResetDefaultCatalogCommand { get; }
    public AsyncRelayCommand RefreshInstalledCommand { get; }
    public AsyncRelayCommand InstallSelectedCommand { get; }
    public RelayCommand CancelInstallCommand { get; }
    public AsyncRelayCommand ScanBloatwareCommand { get; }
    public AsyncRelayCommand RemoveBloatwareCommand { get; }
    public RelayCommand CancelDebloatCommand { get; }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public SelectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                ApplySelectionProfile(value);
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string EtaText
    {
        get => _etaText;
        set => SetProperty(ref _etaText, value);
    }

    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        set => SetProperty(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanManageSelection));
                OnPropertyChanged(nameof(CanEditCatalog));
                OnPropertyChanged(nameof(CanRemoveSelectedApp));
                OnPropertyChanged(nameof(CanDebloat));
            }
        }
    }

    public bool IsWingetAvailable
    {
        get => _isWingetAvailable;
        private set
        {
            if (SetProperty(ref _isWingetAvailable, value))
            {
                RefreshCommandStates();
                OnPropertyChanged(nameof(CanInstall));
            }
        }
    }

    public bool IsSilentInstall
    {
        get => _isSilentInstall;
        set => SetProperty(ref _isSilentInstall, value);
    }

    public bool IsCompactLayout
    {
        get => _isCompactLayout;
        private set
        {
            if (SetProperty(ref _isCompactLayout, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
            }
        }
    }

    public double SidebarWidth => IsCompactLayout ? 200 : 240;
    public bool CanInstall => IsWingetAvailable && !IsBusy;
    public bool CanManageSelection => !IsBusy;
    public bool CanEditCatalog => !IsBusy;
    public bool CanCancelInstall => IsBusy && _installCancellationTokenSource is not null;
    public bool CanRemoveSelectedApp => !IsBusy && Catalog.SelectedItem is not null;
    public bool CanDebloat => !IsBusy;
    public bool CanCancelDebloat => IsBusy && _debloatCancellationTokenSource is not null;
    public string SelectionSummary => Catalog.SelectionSummary;
    public string BloatwareSummary => Debloat.Summary;

    public string StoreSelectionSummary
    {
        get
        {
            var total = Store.SelectedCount + Search.SelectedCount;
            return total > 0 ? $"{total} checked for adding" : "No apps checked";
        }
    }

    public async Task InitializeAsync()
    {
        Store.SetItems(_catalogService.LoadEmbeddedCatalog());
        Debloat.SetItems(_bloatwareService.GetKnownBloatware());
        Catalog.LoadDefaults(MainViewModelPresets.DefaultCatalog, _knownInstalledIds);
        SelectedProfile = SelectionProfiles.First(profile => profile.Key == DefaultProfileKey);
        SetProgress(0, 1);
        UpdateCatalogStatuses();

        var loadCatalogTask = LoadCatalogAsync();
        var initWingetTask = InitializeWingetAsync();
        await Task.WhenAll(loadCatalogTask, initWingetTask);
    }

    public void UpdateLayoutMode(double actualWidth)
    {
        IsCompactLayout = actualWidth < 1220;
    }

    public void AppendLog(string message)
    {
        var prefix = _logBuilder.Length == 0 ? string.Empty : Environment.NewLine;
        _logBuilder.Append(prefix).Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").Append(message);
        LogText = _logBuilder.ToString();
    }

    private async Task InitializeWingetAsync()
    {
        AppendLog("Checking for winget availability...");
        StatusText = "Initializing...";
        try
        {
            var available = await _wingetClient.GetVersionAsync();
            IsWingetAvailable = available is not null;
            if (IsWingetAvailable)
            {
                AppendLog($"winget detected: {available?.Trim()}");
                StatusText = "winget available - ready to install.";
                await RefreshInstalledStatesAsync();
                await DetectBloatwareAsync();
            }
            else
            {
                StatusText = "winget NOT found - install App Installer from the Microsoft Store.";
                AppendLog("ERROR: winget not detected.");
                await DetectBloatwareAsync();
            }
        }
        catch (Exception ex)
        {
            IsWingetAvailable = false;
            StatusText = $"Error checking winget: {ex.Message}";
            AppendLog($"Init error: {ex.Message}");
        }
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            var result = await _catalogService.LoadAsync();
            if (result.Items.Count > 0)
            {
                Store.SetItems(result.Items);
                UpdateCatalogStatuses();
                AppendLog($"Catalog loaded from {result.Source}.");
            }

            foreach (var diagnostic in result.Diagnostics)
            {
                AppendLog($"Catalog note: {diagnostic}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Catalog load failed: {ex.Message}");
        }
    }

    private void ApplySelectionProfile(SelectionProfile profile)
    {
        foreach (var option in SelectionProfiles)
        {
            option.IsSelected = ReferenceEquals(option, profile);
        }

        if (profile.IsCustom)
        {
            return;
        }

        foreach (var wingetId in profile.SelectedWingetIds)
        {
            if (Catalog.AllItems.Any(item => string.Equals(item.WingetId, wingetId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var storeItem = Store.AllItems.FirstOrDefault(item => string.Equals(item.WingetId, wingetId, StringComparison.OrdinalIgnoreCase));
            var name = storeItem?.Name ?? wingetId.Split('.').Last();
            var category = storeItem?.Category ?? "Other";
            Catalog.AddApp(new AppDefinition(name, category, wingetId), _knownInstalledIds);
        }

        Catalog.SetSelectionByProfile(profile.SelectedWingetIds);
        UpdateCatalogStatuses();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void SelectProfile(object? parameter)
    {
        if (parameter is SelectionProfile profile)
        {
            SelectedProfile = profile;
        }
    }

    private void SelectStoreCategory(object? parameter)
    {
        if (parameter is CategoryOption category)
        {
            Store.SelectedCategory = category;
        }
    }

    private void SelectBloatwareCategory(object? parameter)
    {
        if (parameter is CategoryOption category)
        {
            Debloat.SelectedCategory = category;
        }
    }

    private void UpdateCatalogStatuses()
    {
        Store.UpdateCatalogStatus(Catalog.AllItems, _installedInventoryOutput);
        Search.UpdateCatalogStatus(Catalog.AllItems, _installedInventoryOutput);
        OnPropertyChanged(nameof(StoreSelectionSummary));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void UpdateEta(System.Diagnostics.Stopwatch stopwatch, int position, int total)
    {
        if (position <= 0)
        {
            EtaText = "ETA: --";
            return;
        }

        var averageSeconds = stopwatch.Elapsed.TotalSeconds / position;
        var remaining = TimeSpan.FromSeconds(Math.Max(0, averageSeconds * (total - position)));
        EtaText = $"ETA: {remaining:mm\\:ss}";
    }

    private void SetProgress(double value, double maximum)
    {
        ProgressMaximum = Math.Max(1, maximum);
        ProgressValue = value;
    }

    private void ClearLogs()
    {
        _logBuilder.Clear();
        LogText = string.Empty;
    }

    private void RefreshCommandStates()
    {
        StoreSelectAllCommand.RaiseCanExecuteChanged();
        StoreSelectNoneCommand.RaiseCanExecuteChanged();
        SearchSelectAllCommand.RaiseCanExecuteChanged();
        SearchSelectNoneCommand.RaiseCanExecuteChanged();
        DebloatSelectAllCommand.RaiseCanExecuteChanged();
        DebloatSelectNoneCommand.RaiseCanExecuteChanged();
        AddStoreTrendAppCommand.RaiseCanExecuteChanged();
        RemoveSelectedAppCommand.RaiseCanExecuteChanged();
        SearchCommand.RaiseCanExecuteChanged();
        ResetDefaultCatalogCommand.RaiseCanExecuteChanged();
        RefreshInstalledCommand.RaiseCanExecuteChanged();
        InstallSelectedCommand.RaiseCanExecuteChanged();
        CancelInstallCommand.RaiseCanExecuteChanged();
        ScanBloatwareCommand.RaiseCanExecuteChanged();
        RemoveBloatwareCommand.RaiseCanExecuteChanged();
        CancelDebloatCommand.RaiseCanExecuteChanged();
    }

    private void ChildStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is CatalogViewModel)
        {
            OnPropertyChanged(nameof(SelectionSummary));
        }

        if (sender is StoreViewModel or SearchViewModel)
        {
            OnPropertyChanged(nameof(StoreSelectionSummary));
        }

        if (sender is DebloatViewModel)
        {
            OnPropertyChanged(nameof(BloatwareSummary));
        }

        RefreshCommandStates();
    }
}
