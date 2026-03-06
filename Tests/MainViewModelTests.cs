using Initio.Core.Models;
using Initio.Core.ViewModels;

namespace Initio.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsCatalogStoreAndDefaultProfile()
    {
        var viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsWingetAvailable);
        Assert.NotEmpty(viewModel.Catalog.AllItems);
        Assert.NotEmpty(viewModel.Store.AllItems);
        Assert.NotNull(viewModel.SelectedProfile);
        Assert.Equal("default", viewModel.SelectedProfile?.Key);
        Assert.Contains("selected", viewModel.SelectionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(viewModel.Debloat.AllItems, item => item.IsInstalled);
        Assert.Contains(viewModel.Debloat.AllItems, item => item.RemovalStatus == "Detected");
    }

    [Fact]
    public async Task InitializeAsync_MarksStoreAndSearchAppsInstalledEvenWhenOutsideMySetup()
    {
        var storeItems = TestData.CreateStoreItems()
            .Concat([
                new StoreCatalogEntry("Utilities", 99, "PowerToys", "Microsoft.PowerToys", 4.8, "Top ranked")
            ])
            .ToList();
        var catalog = new StubCatalogService(storeItems);
        var winget = new StubWingetClient();
        winget.InstalledTokens.Add("Microsoft.PowerToys");
        winget.InstalledTokens.Add("PowerToys");
        winget.SearchResults = [new WingetSearchResult("PowerToys", "Microsoft.PowerToys")];
        var viewModel = CreateViewModel(catalog: catalog, winget: winget);

        await viewModel.InitializeAsync();

        var storeItem = viewModel.Store.AllItems.Single(item => item.WingetId == "Microsoft.PowerToys");
        Assert.Equal("Installed", storeItem.CatalogStatus);

        viewModel.Search.Query = "power";
        viewModel.SearchCommand.Execute(null);
        await AsyncTestHelper.WaitUntilAsync(() => !viewModel.Search.IsSearching && viewModel.Search.Results.Count > 0);

        var searchItem = viewModel.Search.Results.Single(item => item.WingetId == "Microsoft.PowerToys");
        Assert.Equal("Installed", searchItem.CatalogStatus);
    }

    [Fact]
    public void UpdateLayoutMode_TogglesCompactLayoutAndSidebarWidth()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateLayoutMode(1219);
        Assert.True(viewModel.IsCompactLayout);
        Assert.Equal(200, viewModel.SidebarWidth);

        viewModel.UpdateLayoutMode(1220);
        Assert.False(viewModel.IsCompactLayout);
        Assert.Equal(240, viewModel.SidebarWidth);
    }

    [Fact]
    public async Task SelectingDevProfile_AddsDevelopmentAppsToCatalog()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.SelectedProfile = viewModel.SelectionProfiles.Single(profile => profile.Key == "dev");

        Assert.Contains(viewModel.Catalog.AllItems, item => item.WingetId == "Git.Git");
        Assert.Contains(viewModel.Catalog.AllItems, item => item.WingetId == "Microsoft.VisualStudioCode");
    }

    [Fact]
    public async Task StoreSelectionSummary_TracksStoreAndSearchSelections()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.Store.AllItems[0].IsSelected = true;
        viewModel.Search.SetResults([new WingetSearchResult("PowerToys", "Microsoft.PowerToys")]);
        viewModel.Search.Results[0].IsSelected = true;

        Assert.Equal("2 checked for adding", viewModel.StoreSelectionSummary);

        viewModel.StoreSelectNoneCommand.Execute(null);
        viewModel.SearchSelectNoneCommand.Execute(null);

        Assert.Equal("No apps checked", viewModel.StoreSelectionSummary);
    }

    [Fact]
    public async Task SearchCommand_DisablesSearchActionsWhileRunning()
    {
        var winget = new StubWingetClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winget.SearchHandler = async (_, _, cancellationToken) =>
        {
            await release.Task.WaitAsync(cancellationToken);
            return [new WingetSearchResult("Git", "Git.Git")];
        };
        var viewModel = CreateViewModel(winget: winget);
        await viewModel.InitializeAsync();
        viewModel.Search.Query = "git";

        viewModel.SearchCommand.Execute(null);
        await AsyncTestHelper.WaitUntilAsync(() => viewModel.Search.IsSearching);

        Assert.False(viewModel.SearchCommand.CanExecute(null));
        Assert.False(viewModel.SearchSelectAllCommand.CanExecute(null));

        release.SetResult();
        await AsyncTestHelper.WaitUntilAsync(() => !viewModel.Search.IsSearching);

        Assert.Single(viewModel.Search.Results);
        Assert.Equal("Git.Git", viewModel.Search.Results[0].WingetId);
    }

    [Fact]
    public async Task InstallSelectedCommand_RetriesOnceThenSucceeds()
    {
        var winget = new StubWingetClient();
        winget.QueueInstallResponses("Mozilla.Firefox", null, "Successfully installed");
        var viewModel = CreateViewModel(winget: winget);
        await viewModel.InitializeAsync();
        SelectOnlyCatalogItem(viewModel, "Mozilla.Firefox");

        viewModel.InstallSelectedCommand.Execute(null);

        var firefox = viewModel.Catalog.AllItems.Single(item => item.WingetId == "Mozilla.Firefox");
        await AsyncTestHelper.WaitUntilAsync(() => winget.GetInstallAttempts("Mozilla.Firefox") == 2 && firefox.IsInstalled && !viewModel.IsBusy);

        Assert.Equal(2, winget.GetInstallAttempts("Mozilla.Firefox"));
        Assert.True(firefox.IsInstalled);
        Assert.Equal("Installed", firefox.InstallStatus);
        Assert.True(viewModel.CanInstall);
        Assert.False(viewModel.CanCancelInstall);
    }

    [Fact]
    public async Task CancelInstallCommand_StopsActiveInstallAndRestoresState()
    {
        var winget = new StubWingetClient();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winget.InstallHandler = async (_, _, _, cancellationToken) =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return "Successfully installed";
        };
        var viewModel = CreateViewModel(winget: winget);
        await viewModel.InitializeAsync();
        SelectOnlyCatalogItem(viewModel, "Mozilla.Firefox");

        viewModel.InstallSelectedCommand.Execute(null);
        await AsyncTestHelper.WaitUntilAsync(() => viewModel.CanCancelInstall);

        Assert.False(viewModel.CanInstall);
        viewModel.CancelInstallCommand.Execute(null);
        await AsyncTestHelper.WaitUntilAsync(() => !viewModel.IsBusy);

        Assert.Contains("cancel", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.CanCancelInstall);
        Assert.True(viewModel.CanInstall);
    }

    [Fact]
    public async Task RemoveBloatwareCommand_RemovesDetectedPackagesAndUpdatesSummary()
    {
        var bloatware = new StubBloatwareService();
        var viewModel = CreateViewModel(bloatware: bloatware);
        await viewModel.InitializeAsync();
        viewModel.DebloatSelectAllCommand.Execute(null);

        viewModel.RemoveBloatwareCommand.Execute(null);
        await AsyncTestHelper.WaitUntilAsync(() => viewModel.Debloat.AllItems.Where(item => item.PackageName != "Microsoft.XboxGamingOverlay").All(item => item.RemovalStatus == "Removed"));

        Assert.All(viewModel.Debloat.AllItems.Where(item => item.PackageName != "Microsoft.XboxGamingOverlay"), item =>
        {
            Assert.False(item.IsInstalled);
            Assert.Equal("Removed", item.RemovalStatus);
        });
        Assert.Contains("2 removed", viewModel.BloatwareSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static MainViewModel CreateViewModel(StubCatalogService? catalog = null, StubWingetClient? winget = null, StubBloatwareService? bloatware = null)
    {
        return new MainViewModel(
            catalog ?? new StubCatalogService(),
            winget ?? new StubWingetClient(),
            bloatware ?? new StubBloatwareService());
    }

    private static void SelectOnlyCatalogItem(MainViewModel viewModel, string wingetId)
    {
        foreach (var item in viewModel.Catalog.AllItems)
        {
            item.IsSelected = string.Equals(item.WingetId, wingetId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
