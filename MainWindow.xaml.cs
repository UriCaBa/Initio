// MainWindow.xaml.cs
// Primary code-behind for MainWindow — handles UI initialization, theme switching,
// profile selection, catalog management, and property change notifications.
// Installation logic is split into MainWindow.Install.cs (partial class).

using NewPCSetupWPF.Models;
using NewPCSetupWPF.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace NewPCSetupWPF;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string ThemePrefix = "/Themes/Theme.";
    private const string DefaultProfileKey = "default";
    private const string CustomProfileKey = "custom";
    private static readonly Regex WingetIdPattern = new(@"^[A-Za-z0-9][A-Za-z0-9\.\-\+_]*$", RegexOptions.Compiled);

    /// <summary>Default app catalog — the user's chosen defaults for a fresh PC.</summary>
    private static readonly IReadOnlyList<AppDefinition> DefaultCatalog =
    [
        new("Chrome", "Browsers", "Google.Chrome"),
        new("Firefox", "Browsers", "Mozilla.Firefox"),
        new("Spotify", "Media", "Spotify.Spotify"),
        new("VLC", "Media", "VideoLAN.VLC"),
        new("Discord", "Comms", "Discord.Discord"),
        new("Steam", "Gaming", "Valve.Steam"),
        new("Dropbox", "Cloud", "Dropbox.Dropbox"),
        new("LibreOffice", "Docs", "TheDocumentFoundation.LibreOffice"),
        new("NVIDIA App", "Drivers", "Nvidia.NVIDIAApp"),
        new("Notepad++", "Utilities", "Notepad++.Notepad++"),
        new("Riot Vanguard", "Gaming", "RiotGames.RiotClient"),
        new("Foxit PDF Reader", "Docs", "Foxit.FoxitReader")
    ];

    // ═══ Collections & State ═══
    private readonly ObservableCollection<AppItem> _appItems = new();
    private readonly ObservableCollection<StoreTrendItem> _storeTrendItems = new();
    private readonly ObservableCollection<StoreTrendItem> _wingetSearchResults = new();
    private readonly ObservableCollection<BloatwareItem> _bloatwareItems = new();
    private string _wingetSearchQuery = string.Empty;
    private bool _isSearching;
    private readonly ReadOnlyCollection<ThemeOption> _themeOptions;
    private readonly ReadOnlyCollection<SelectionProfile> _selectionProfiles;
    private ReadOnlyCollection<string> _storeCategories = new(Array.Empty<string>());
    private ReadOnlyCollection<string> _bloatwareCategories = new(Array.Empty<string>());
    private string _selectedBloatwareCategory = "All";
    private readonly HashSet<string> _knownInstalledIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _logBuilder = new();
    private readonly object _logSync = new();

    // ═══ Backing Fields ═══
    private bool _isApplyingProfile;
    private string _statusText = "Ready.";
    private string _etaText = "ETA: --";
    private double _progressMaximum = 1;
    private double _progressValue;
    private bool _isBusy;
    private bool _isWingetAvailable;
    private ThemeOption? _selectedTheme;
    private SelectionProfile? _selectedProfile;
    private AppItem? _selectedAppItem;
    private StoreTrendItem? _selectedStoreTrendItem;
    private string _storeTrendQuery = string.Empty;
    private string _selectedStoreCategory = string.Empty;
    private string _logText = "Logs will appear here when you run an installation.";
    internal bool _hasInstallSession;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Theme options — each maps to a distinct ResourceDictionary
        _themeOptions = new ReadOnlyCollection<ThemeOption>(
        [
            new ThemeOption("Midnight Blue", "/Themes/Theme.DarkElegant.xaml"),
            new ThemeOption("Neon Cyberpunk", "/Themes/Theme.GamerRgb.xaml"),
            new ThemeOption("Slate Professional", "/Themes/Theme.Corporate.xaml"),
            new ThemeOption("Gemini AI", "/Themes/Theme.Gemini.xaml"),
            new ThemeOption("Hacker Terminal", "/Themes/Theme.Hacker.xaml")
        ]);

        _selectionProfiles = CreateSelectionProfiles();
        _appItems.CollectionChanged += AppItems_CollectionChanged;
        AppItemsView = CollectionViewSource.GetDefaultView(_appItems);
        AppItemsView.Filter = FilterAppItem;

        // Sort: pending apps first (IsInstalled ascending), then alphabetical by Name
        AppItemsView.SortDescriptions.Add(new SortDescription(nameof(AppItem.IsInstalled), ListSortDirection.Ascending));
        AppItemsView.SortDescriptions.Add(new SortDescription(nameof(AppItem.Name), ListSortDirection.Ascending));

        StoreTrendItemsView = CollectionViewSource.GetDefaultView(_storeTrendItems);
        StoreTrendItemsView.Filter = FilterStoreTrendItem;
        WingetSearchResultsView = CollectionViewSource.GetDefaultView(_wingetSearchResults);

        // Initialize bloatware collection and view
        BloatwareItemsView = CollectionViewSource.GetDefaultView(_bloatwareItems);
        BloatwareItemsView.Filter = FilterBloatwareItem;
        BloatwareItemsView.SortDescriptions.Add(new SortDescription(nameof(BloatwareItem.Category), ListSortDirection.Ascending));
        BloatwareItemsView.SortDescriptions.Add(new SortDescription(nameof(BloatwareItem.Name), ListSortDirection.Ascending));
        PopulateBloatwareItems();

        // Populate store catalog with embedded JSON fallback (instant, no network)
        PopulateStoreItems(Services.CatalogService.LoadEmbeddedCatalog());

        // Load defaults — all default apps are pre-selected
        ReloadDefaultCatalog();
        UpdateStoreCatalogStatus();
        SelectedTheme = _themeOptions[0];
        SelectedProfile = GetProfile(DefaultProfileKey);
        SelectedStoreCategory = _storeCategories.Count > 0 ? _storeCategories[0] : string.Empty;
        _hasInstallSession = false;
        SetProgress(0, 1);
        UpdateProfilePillStyles();
        BuildCategoryTabs();
        BuildBloatwareCategoryTabs();

        // Fire-and-forget: try to load the remote catalog in the background
        _ = LoadCatalogAsync();
    }

    // ═══ Public Properties ═══
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<AppItem> AppItems => _appItems;
    public ICollectionView AppItemsView { get; }
    public ICollectionView StoreTrendItemsView { get; }
    public ICollectionView WingetSearchResultsView { get; }
    public ICollectionView BloatwareItemsView { get; }
    public ReadOnlyCollection<ThemeOption> ThemeOptions => _themeOptions;
    public ReadOnlyCollection<SelectionProfile> SelectionProfiles => _selectionProfiles;
    public ReadOnlyCollection<string> StoreCategories => _storeCategories;
    public ReadOnlyCollection<string> BloatwareCategories => _bloatwareCategories;

    public string SelectedBloatwareCategory
    {
        get => _selectedBloatwareCategory;
        set
        {
            if (SetField(ref _selectedBloatwareCategory, value))
            {
                BloatwareItemsView.Refresh();
                UpdateBloatwareCategoryTabStyles();
            }
        }
    }

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
                UpdateProfilePillStyles();
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

    public StoreTrendItem? SelectedStoreTrendItem
    {
        get => _selectedStoreTrendItem;
        set
        {
            if (SetField(ref _selectedStoreTrendItem, value))
            {
                OnPropertyChanged(nameof(CanAddStoreTrendApp));
            }
        }
    }

    public string StoreTrendQuery
    {
        get => _storeTrendQuery;
        set
        {
            if (SetField(ref _storeTrendQuery, value))
            {
                StoreTrendItemsView.Refresh();
            }
        }
    }

    public string SelectedStoreCategory
    {
        get => _selectedStoreCategory;
        set
        {
            if (SetField(ref _selectedStoreCategory, value))
            {
                StoreTrendItemsView.Refresh();
                UpdateCategoryTabStyles();
            }
        }
    }

    private bool _isSilentInstall = false;
    public bool IsSilentInstall
    {
        get => _isSilentInstall;
        set => SetField(ref _isSilentInstall, value);
    }

    public string WingetSearchQuery
    {
        get => _wingetSearchQuery;
        set => SetField(ref _wingetSearchQuery, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetField(ref _isSearching, value);
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
            if (!_hasInstallSession) return "--";
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
                OnPropertyChanged(nameof(CanAddStoreTrendApp));
                OnPropertyChanged(nameof(CanDebloat));
                OnPropertyChanged(nameof(CanCancelDebloat));
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

    // ═══ Computed Booleans ═══
    public bool CanInstall => IsWingetAvailable && !IsBusy;
    public bool CanManageSelection => !IsBusy;
    public bool CanEditCatalog => !IsBusy;
    public bool CanCancelInstall => IsBusy && _installCancellationTokenSource is not null;
    public bool CanRemoveSelectedApp => !IsBusy && SelectedAppItem is not null;
    public bool CanAddStoreTrendApp => !IsBusy && SelectedStoreTrendItem is not null;
    public bool CanDebloat => !IsBusy;
    public bool CanCancelDebloat => IsBusy && _debloatCancellationTokenSource is not null;

    public string BloatwareSummary
    {
        get
        {
            var detected = _bloatwareItems.Count(x => x.IsInstalled);
            var selected = _bloatwareItems.Count(x => x.IsSelected && x.IsInstalled);
            var removed = _bloatwareItems.Count(x => !x.IsInstalled && x.RemovalStatus == "Removed");
            return $"{selected} selected · {detected} detected · {removed} removed";
        }
    }

    public string SelectionSummary
    {
        get
        {
            var installed = _appItems.Count(x => x.IsInstalled);
            var selectable = _appItems.Count - installed;
            var selected = _appItems.Count(x => x.IsSelected && !x.IsInstalled);
            return $"{selected} selected · {selectable} available · {installed} installed";
        }
    }

    /// <summary>Shows how many apps are checked in the Store tabs (Store by Category + Full Search).</summary>
    public string StoreSelectionSummary
    {
        get
        {
            var storeChecked = _storeTrendItems.Count(x => x.IsSelected);
            var searchChecked = _wingetSearchResults.Count(x => x.IsSelected);
            var total = storeChecked + searchChecked;
            return total > 0 ? $"{total} checked for adding" : "No apps checked";
        }
    }

    // ═══ Window Chrome Events ═══
    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            WingetSearch_Click(sender, e);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        DragMove();
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void WindowMaxRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    // ═══ Profile Pill Button Click Handlers ═══
    private void ProfileDefault_Click(object sender, RoutedEventArgs e) => SelectedProfile = GetProfile(DefaultProfileKey);
    private void ProfileDev_Click(object sender, RoutedEventArgs e) => SelectedProfile = GetProfile("dev");
    private void ProfileHomeOffice_Click(object sender, RoutedEventArgs e) => SelectedProfile = GetProfile("homeoffice");
    private void ProfileGaming_Click(object sender, RoutedEventArgs e) => SelectedProfile = GetProfile("gaming");
    private void ProfileCustom_Click(object sender, RoutedEventArgs e) => SelectedProfile = GetProfile(CustomProfileKey);

    /// <summary>Updates pill button styles to highlight the active profile.</summary>
    private void UpdateProfilePillStyles()
    {
        var activeStyle = (Style)FindResource("ProfilePillActiveStyle");
        var inactiveStyle = (Style)FindResource("ProfilePillStyle");
        var key = SelectedProfile?.Key ?? "";

        if (ProfileDefault != null) ProfileDefault.Style = key == DefaultProfileKey ? activeStyle : inactiveStyle;
        if (ProfileDev != null) ProfileDev.Style = key == "dev" ? activeStyle : inactiveStyle;
        if (ProfileHomeOffice != null) ProfileHomeOffice.Style = key == "homeoffice" ? activeStyle : inactiveStyle;
        if (ProfileGaming != null) ProfileGaming.Style = key == "gaming" ? activeStyle : inactiveStyle;
        if (ProfileCustom != null) ProfileCustom.Style = key == CustomProfileKey ? activeStyle : inactiveStyle;
    }

    // ═══ Catalog Action Handlers ═══

    /// <summary>Select all checkboxes in Store by Category tab.</summary>
    private void StoreSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (StoreTrendItem item in StoreTrendItemsView)
            item.IsSelected = true;
    }

    /// <summary>Deselect all checkboxes in Store by Category tab.</summary>
    private void StoreSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _storeTrendItems)
            item.IsSelected = false;
    }

    /// <summary>Select all checkboxes in Full Search tab.</summary>
    private void SearchSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _wingetSearchResults)
            item.IsSelected = true;
    }

    /// <summary>Deselect all checkboxes in Full Search tab.</summary>
    private void SearchSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _wingetSearchResults)
            item.IsSelected = false;
    }

    /// <summary>Live search winget repo and populate the Full Search tab results.</summary>
    private async void WingetSearch_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WingetSearchQuery))
        {
            AppendLog("Enter a search query first.");
            return;
        }

        IsSearching = true;
        AppendLog($"Searching winget for '{WingetSearchQuery}'...");

        try
        {
            var results = await WingetSearchService.SearchAsync(WingetSearchQuery, 60);
            _wingetSearchResults.Clear();

            int rank = 0;
            foreach (var result in results)
            {
                rank++;
                var item = new StoreTrendItem("Search Result", rank, result.Name, result.WingetId, 0, "winget");

                // Mark as already in catalog if applicable
                if (_appItems.Any(a => string.Equals(a.WingetId, result.WingetId, StringComparison.OrdinalIgnoreCase)))
                    item.IsSelected = false; // Don't auto-select items already in catalog

                // Subscribe to changes for Summary updates
                item.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(StoreTrendItem.IsSelected))
                        OnPropertyChanged(nameof(StoreSelectionSummary)); // Use the property bound to footer
                };

                _wingetSearchResults.Add(item);
            }

            WingetSearchResultsView.Refresh();
            var msg = results.Count > 0
                ? $"Found {results.Count} result(s) for '{WingetSearchQuery}'."
                : $"No results found for '{WingetSearchQuery}'.";
            AppendLog(msg);
        }
        catch (Exception ex)
        {
            AppendLog($"Search failed: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }



    /// <summary>Updates CatalogStatus on Store items to show "In My Setup", "Installed", or empty.</summary>
    private void UpdateStoreCatalogStatus(string? wingetOutput = null)
    {
        foreach (var storeItem in _storeTrendItems)
        {
            // 1. Check if it's already in "My Setup"
            var match = _appItems.FirstOrDefault(a =>
                string.Equals(a.WingetId, storeItem.WingetId, StringComparison.OrdinalIgnoreCase));
            
            if (match is not null)
            {
                storeItem.CatalogStatus = match.IsInstalled ? "✅ Installed" : "📋 In My Setup";
            }
            else
            {
                // 2. If not in My Setup, check if it's installed on the system (using winget output)
                bool isInstalled = false;
                if (!string.IsNullOrEmpty(wingetOutput))
                {
                    isInstalled = wingetOutput.Contains(storeItem.WingetId, StringComparison.OrdinalIgnoreCase);
                    if (!isInstalled && !string.IsNullOrWhiteSpace(storeItem.Name))
                    {
                         // Fallback: Check by Name (for Store vs Winget ID mismatches)
                         isInstalled = wingetOutput.Contains(storeItem.Name, StringComparison.OrdinalIgnoreCase);
                    }
                }
                
                storeItem.CatalogStatus = isInstalled ? "✅ Installed" : string.Empty;
            }
        }

        // Also update search results if any
        foreach (var item in _wingetSearchResults)
        {
            var match = _appItems.FirstOrDefault(a =>
                string.Equals(a.WingetId, item.WingetId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                item.CatalogStatus = match.IsInstalled ? "✅ Installed" : "📋 In My Setup";
            else
                item.CatalogStatus = string.Empty;
        }
    }



    /// <summary>Adds all checked (IsSelected=true) items from BOTH Store and Search results into the install catalog.</summary>
    private void AddStoreTrendApp_Click(object sender, RoutedEventArgs e)
    {
        // Gather from Store Catalog
        var storeSelected = _storeTrendItems.Where(s => s.IsSelected).ToList();
        // Gather from Search Results
        var searchSelected = _wingetSearchResults.Where(s => s.IsSelected).ToList();

        var allSelected = storeSelected.Concat(searchSelected).Distinct().ToList();

        if (allSelected.Count == 0) { AppendLog("Check one or more apps to add."); return; }

        int added = 0;
        int skipped = 0;

        foreach (var item in allSelected)
        {
            if (_appItems.Any(x => string.Equals(x.WingetId, item.WingetId, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
            }
            else
            {
                AddAppToCatalog(new AppDefinition(item.Name, item.Category ?? "Custom", item.WingetId));
                added++;
                AppendLog($"App added: {item.Name} ({item.WingetId})");
            }
            item.IsSelected = false; // Uncheck after processing
        }

        if (added > 0)
        {
            AppItemsView.Refresh();
            OnPropertyChanged(nameof(SelectionSummary));
            UpdateStoreCatalogStatus();
        }

        var msg = added > 0
            ? $"Added {added} app(s)." + (skipped > 0 ? $" ({skipped} already in catalog)" : "")
            : $"All {skipped} app(s) already in catalog.";
        AppendLog(msg);
    }

    private void RemoveSelectedApp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAppItem is null) { AppendLog("Select an app to remove."); return; }
        var app = SelectedAppItem;
        _appItems.Remove(app);
        _knownInstalledIds.Remove(app.WingetId);
        SelectedAppItem = null;
        AppItemsView.Refresh();
        AppendLog($"Removed: {app.Name} ({app.WingetId})");
        AppendLog($"Removed: {app.Name}.");
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private async void ResetDefaultCatalog_Click(object sender, RoutedEventArgs e)
    {
        ReloadDefaultCatalog();
        SelectedProfile = GetProfile(DefaultProfileKey);
        AppendLog("Default catalog restored.");
        AppendLog("Default catalog restored.");
        if (IsWingetAvailable) await RefreshInstalledStatesAsync();
    }

    // ═══ Theme System ═══
    private void ApplyTheme(string resourcePath)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var current = merged.FirstOrDefault(d =>
            d.Source is not null &&
            d.Source.OriginalString.StartsWith(ThemePrefix, StringComparison.OrdinalIgnoreCase));
        if (current is not null) merged.Remove(current);
        merged.Add(new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Relative) });
    }

    // ═══ Profile System ═══
    private void ApplySelectionProfile(SelectionProfile profile)
    {
        if (profile.IsCustom) return;
        _isApplyingProfile = true;
        try
        {
            // First add any apps from the profile that are not already in the catalog
            foreach (var wingetId in profile.SelectedWingetIds)
            {
                if (!_appItems.Any(x => string.Equals(x.WingetId, wingetId, StringComparison.OrdinalIgnoreCase)))
                {
                    // Try to find app info from store catalog, otherwise use the ID as name
                    var storeItem = _storeTrendItems.FirstOrDefault(s =>
                        string.Equals(s.WingetId, wingetId, StringComparison.OrdinalIgnoreCase));
                    var name = storeItem?.Name ?? wingetId.Split('.').Last();
                    var category = storeItem?.Category ?? "Other";
                    AddAppToCatalog(new AppDefinition(name, category, wingetId));
                }
            }

            // Now set selection state for all apps
            foreach (var app in _appItems)
            {
                // Set IsSelected purely based on profile match, ignoring install status
                // This keeps checkboxes consistent with the profile definition
                app.IsSelected = profile.SelectedWingetIds.Contains(app.WingetId);
            }
        }
        finally { _isApplyingProfile = false; }
        AppItemsView.Refresh();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    /// <summary>Builds all selection profiles including Home Office and Gaming.</summary>
    private ReadOnlyCollection<SelectionProfile> CreateSelectionProfiles()
    {
        var defaultIds = new HashSet<string>(DefaultCatalog.Select(x => x.WingetId), StringComparer.OrdinalIgnoreCase);

        // Dev — defaults + developer tools
        var devRig = new HashSet<string>(defaultIds, StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.VisualStudioCode", "Git.Git",
            "Docker.DockerDesktop", "OBSProject.OBSStudio",
            "GitHub.GitHubDesktop"
        };

        // Home Office — remote work, docs, cloud storage, video calls
        var homeOffice = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google.Chrome", "Mozilla.Firefox", "Zoom.Zoom",
            "Microsoft.Teams", "SlackTechnologies.Slack",
            "TheDocumentFoundation.LibreOffice", "Foxit.FoxitReader",
            "Dropbox.Dropbox", "Notion.Notion",
            "Discord.Discord", "Notepad++.Notepad++", "Spotify.Spotify"
        };

        // Gaming — launchers, drivers, streaming
        var gaming = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Valve.Steam", "EpicGames.EpicGamesLauncher", "GOG.Galaxy",
            "RiotGames.RiotClient", "Discord.Discord",
            "Nvidia.NVIDIAApp", "OBSProject.OBSStudio",
            "Google.Chrome", "Spotify.Spotify"
        };

        return new ReadOnlyCollection<SelectionProfile>(
        [
            new SelectionProfile(DefaultProfileKey, "Default (My Setup)", defaultIds),
            new SelectionProfile("dev", "Dev Workstation", devRig),
            new SelectionProfile("homeoffice", "Home Office", homeOffice),
            new SelectionProfile("gaming", "Gaming Rig", gaming),
            new SelectionProfile(CustomProfileKey, "Custom", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true)
        ]);
    }

    // ═══ Catalog Helpers ═══
    private void ReloadDefaultCatalog()
    {
        _appItems.Clear();
        foreach (var app in DefaultCatalog) AddAppToCatalog(app);
        // Select all default apps for installation
        foreach (var app in _appItems) app.IsSelected = !app.IsInstalled;
        AppItemsView.Refresh();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    /// <summary>Adds an app to the catalog and marks it as selected for install.</summary>
    private void AddAppToCatalog(AppDefinition definition)
    {
        if (_appItems.Any(x => string.Equals(x.WingetId, definition.WingetId, StringComparison.OrdinalIgnoreCase)))
            return;
        var app = new AppItem { Name = definition.Name, Category = definition.Category, WingetId = definition.WingetId };
        app.IsSelected = true; // All apps in catalog are selected by default (visible in My Setup)
        if (_knownInstalledIds.Contains(definition.WingetId))
        {
            app.IsInstalled = true;
            app.InstallStatus = "Installed";
        }
        _appItems.Add(app);
    }

    private SelectionProfile GetProfile(string key) => _selectionProfiles.First(p => p.Key == key);

    // ═══ Filtering ═══
    /// <summary>My Setup tab filter: only show apps that are selected (including installed that user hasn't hidden).</summary>
    private bool FilterAppItem(object item)
    {
        if (item is not AppItem app) return false;
        // Show if selected OR installed (so installed apps don't disappear)
        return app.IsSelected || app.IsInstalled;
    }

    private bool FilterStoreTrendItem(object item)
    {
        if (item is not StoreTrendItem app) return false;
        if (!string.IsNullOrEmpty(SelectedStoreCategory) &&
            !string.Equals(app.Category, SelectedStoreCategory, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(StoreTrendQuery)) return true;
        var term = StoreTrendQuery.Trim();
        return app.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.WingetId.Contains(term, StringComparison.OrdinalIgnoreCase)
            || app.PopularitySignal.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    // ═══ Bloatware Filtering & Init ═══
    private bool FilterBloatwareItem(object item)
    {
        if (item is not BloatwareItem bloat) return false;
        if (!bloat.IsInstalled && bloat.RemovalStatus != "Removed") return false;
        if (SelectedBloatwareCategory == "All") return true;
        return string.Equals(bloat.Category, SelectedBloatwareCategory, StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateBloatwareItems()
    {
        _bloatwareItems.Clear();
        foreach (var item in BloatwareService.GetKnownBloatware())
        {
            item.PropertyChanged += BloatwareItem_PropertyChanged;
            _bloatwareItems.Add(item);
        }

        _bloatwareCategories = new ReadOnlyCollection<string>(
            _bloatwareItems.Select(i => i.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Prepend("All")
                .ToList());
    }

    private void BloatwareItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BloatwareItem.IsSelected) or nameof(BloatwareItem.IsInstalled))
        {
            OnPropertyChanged(nameof(BloatwareSummary));
        }
    }

    private void BuildBloatwareCategoryTabs()
    {
        if (BloatwareCategoryTabsPanel == null) return;
        BloatwareCategoryTabsPanel.Children.Clear();

        foreach (var category in _bloatwareCategories)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = category,
                Tag = category,
                Margin = new Thickness(0, 0, 5, 5)
            };
            btn.Click += BloatwareCategoryTab_Click;
            BloatwareCategoryTabsPanel.Children.Add(btn);
        }
        UpdateBloatwareCategoryTabStyles();
    }

    private void BloatwareCategoryTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string category)
            SelectedBloatwareCategory = category;
    }

    private void UpdateBloatwareCategoryTabStyles()
    {
        if (BloatwareCategoryTabsPanel == null) return;
        var activeStyle = (Style)FindResource("CategoryTabActiveStyle");
        var inactiveStyle = (Style)FindResource("CategoryTabStyle");

        foreach (var child in BloatwareCategoryTabsPanel.Children)
        {
            if (child is System.Windows.Controls.Button btn && btn.Tag is string tag)
            {
                btn.Style = string.Equals(tag, SelectedBloatwareCategory, StringComparison.OrdinalIgnoreCase)
                    ? activeStyle
                    : inactiveStyle;
            }
        }
    }

    // ═══ Collection Observers ═══
    private void AppItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (AppItem app in e.NewItems) app.PropertyChanged += AppItem_PropertyChanged;
        if (e.OldItems is not null)
            foreach (AppItem app in e.OldItems) app.PropertyChanged -= AppItem_PropertyChanged;
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AppItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppItem.IsSelected) or nameof(AppItem.IsInstalled))
        {
            OnPropertyChanged(nameof(SelectionSummary));
            // Skip view refresh during bulk profile application to avoid O(n) refreshes
            if (!_isApplyingProfile)
                AppItemsView.Refresh();
        }
    }

    // ═══ Category Tab System (horizontal pills in Store tab) ═══
    /// <summary>Populates the CategoryTabsPanel WrapPanel with pill buttons for each store category.</summary>
    private void BuildCategoryTabs()
    {
        if (CategoryTabsPanel == null) return;
        CategoryTabsPanel.Children.Clear();

        foreach (var category in _storeCategories)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = category == "All categories" ? "All" : category,
                Tag = category,
                Margin = new Thickness(0, 0, 5, 5)
            };
            btn.Click += CategoryTab_Click;
            CategoryTabsPanel.Children.Add(btn);
        }
        UpdateCategoryTabStyles();
    }

    private void CategoryTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string category)
        {
            SelectedStoreCategory = category;
        }
    }

    /// <summary>Applies active/inactive styles to category tab buttons based on current selection.</summary>
    private void UpdateCategoryTabStyles()
    {
        if (CategoryTabsPanel == null) return;
        var activeStyle = (Style)FindResource("CategoryTabActiveStyle");
        var inactiveStyle = (Style)FindResource("CategoryTabStyle");

        foreach (var child in CategoryTabsPanel.Children)
        {
            if (child is System.Windows.Controls.Button btn && btn.Tag is string tag)
            {
                btn.Style = string.Equals(tag, SelectedStoreCategory, StringComparison.OrdinalIgnoreCase)
                    ? activeStyle
                    : inactiveStyle;
            }
        }
    }

    // ═══ Remote Catalog Loading ═══

    /// <summary>
    /// Populates the store with items from a catalog source, subscribing to property changes.
    /// Rebuilds category tabs and resets selection.
    /// </summary>
    private void PopulateStoreItems(IReadOnlyList<StoreTrendItem> items)
    {
        _storeTrendItems.Clear();
        foreach (var item in items)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StoreTrendItem.IsSelected))
                    OnPropertyChanged(nameof(StoreSelectionSummary));
            };
            _storeTrendItems.Add(item);
        }

        var categories = _storeTrendItems
            .Select(i => i.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _storeCategories = new ReadOnlyCollection<string>(categories);
    }

    /// <summary>
    /// Attempts to load the catalog from the remote JSON (GitHub).
    /// If successful, replaces the current store items with the remote data.
    /// On failure, the embedded fallback loaded in the constructor remains active.
    /// </summary>
    private async Task LoadCatalogAsync()
    {
        try
        {
            var (items, source) = await Services.CatalogService.LoadAsync().ConfigureAwait(false);

            // Only update if we got data from remote or cache (not embedded, which is already loaded)
            if (source != "embedded" && items.Count > 0)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    PopulateStoreItems(items);
                    SelectedStoreCategory = _storeCategories.Count > 0 ? _storeCategories[0] : string.Empty;
                    UpdateStoreCatalogStatus();
                    StoreTrendItemsView.Refresh();
                    BuildCategoryTabs();
                    AppendLog($"📦 Store catalog updated ({source}, {items.Count} apps).");
                });
            }
        }
        catch
        {
            // Non-critical — embedded fallback is already active from the constructor
        }
    }

    // ═══ Logging ═══
    internal void ClearLogs()
    {
        if (Dispatcher.CheckAccess()) { ClearLogsCore(); return; }
        Dispatcher.Invoke(ClearLogsCore);
    }

    private void ClearLogsCore()
    {
        lock (_logSync) { _logBuilder.Clear(); LogText = string.Empty; }
    }

    internal void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        if (Dispatcher.CheckAccess()) { AppendLogCore(line); return; }
        Dispatcher.Invoke(() => AppendLogCore(line));
    }

    internal void AppendLogCore(string line)
    {
        lock (_logSync) { _logBuilder.AppendLine(line); LogText = _logBuilder.ToString(); }
        // Auto-scroll logic is handled by binding or code-behind event, 
        // but since we're binding Text, we need the TextBox to scroll.
        // We'll expose an event or use the LogTextBox name if accessible.
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.LogTextBox != null)
            {
                mw.LogTextBox.ScrollToEnd();
            }
        });
    }

    // ═══ Progress Helpers ═══
    internal void SetProgress(double value, double maximum)
    {
        ProgressMaximum = maximum <= 0 ? 1 : maximum;
        ProgressValue = value;
    }

    // ═══ INotifyPropertyChanged ═══
    internal void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    // ═══ Records ═══
    public sealed record SelectionProfile(string Key, string DisplayName, HashSet<string> SelectedWingetIds, bool IsCustom = false);
    public sealed record ThemeOption(string DisplayName, string ResourcePath);
    internal sealed record AppDefinition(string Name, string Category, string WingetId);
}
