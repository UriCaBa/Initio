using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Initio.UITests;

[Collection("InitioApp")]
public class InitioAppTests : IDisposable
{
    private static readonly string ExePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "bin", "Debug", "net8.0-windows", "win-x64", "Initio.exe"));

    private readonly UIA3Automation _automation;
    private Application? _app;
    private readonly ITestOutputHelper _output;

    public InitioAppTests(ITestOutputHelper output)
    {
        _automation = new UIA3Automation();
        _output = output;
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { }
        try { _app?.Dispose(); } catch { }
        _automation.Dispose();
    }

    private Window LaunchAndGetMainWindow(int timeoutSeconds = 15)
    {
        Assert.True(File.Exists(ExePath), $"Initio.exe not found at: {ExePath}");
        _app = Application.Launch(ExePath);
        var window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(timeoutSeconds));
        Assert.NotNull(window);
        window.Focus();
        // Give it a moment to render content
        Thread.Sleep(2000); 
        return window;
    }

    private AutomationElement WaitForElement(AutomationElement parent, string automationId, int timeoutMs = 5000)
    {
        var result = Retry.WhileNull(
            () => parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(500));

        if (result.Result == null)
        {
            _output.WriteLine($"TIMEOUT: Could not find AutomationId='{automationId}' under '{parent.Name}'.");
            Assert.Fail($"Element with AutomationId '{automationId}' not found.");
        }
        return result.Result;
    }

    [Fact]
    public void CatalogTab_ShowsAppItems()
    {
        var window = LaunchAndGetMainWindow();
        
        // 1. Find Tab Control
        var mainTabs = WaitForElement(window, "MainTabControl").AsTab();
        Assert.NotNull(mainTabs);

        // 2. Select First Tab (My Setup)
        // Accessing TabItems might require waiting if they are lazy loaded? 
        // But mainTabs.TabItems should be populated.
        var mySetupTab = mainTabs.TabItems.FirstOrDefault(t => t.AutomationId == "CatalogTab");
        Assert.NotNull(mySetupTab);
        mySetupTab.Select();
        Thread.Sleep(1000); // Wait for content switch

        // 3. Find ListView inside the tab
        // Note: Based on diagnostics, the ListView appears as a child of the TabItem in UIA tree.
        // We search from 'window' to be safe, or 'mainTabs'.
        var listView = WaitForElement(window, "CatalogListView").AsDataGridView();
        Assert.NotNull(listView);

        // 4. Verify Items
        // Rows might take a moment to bind
        Retry.WhileEmpty(() => listView.Rows, TimeSpan.FromSeconds(5));
        
        Assert.True(listView.Rows.Length > 0, "CatalogListView should have items (AppItems)");
        
        foreach (var row in listView.Rows)
        {
            Assert.NotNull(row);
             // Basic verification of row content if needed
        }
    }
}
