using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Initio.UITests;

[Collection("InitioApp")]
public class InitioAppTests : IDisposable
{
    private static readonly string ExePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "bin", "Debug", "net8.0-windows", "win-x64", "Initio.exe"));

    private readonly UIA3Automation _automation = new();
    private readonly ITestOutputHelper _output;
    private Application? _app;

    public InitioAppTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { }
        try { _app?.Dispose(); } catch { }
        _automation.Dispose();
    }

    [Fact]
    public void Shell_LoadsAndShowsCoreControls()
    {
        var window = LaunchAndGetMainWindow();

        Assert.NotNull(WaitForElement(window, "ThemeComboBox"));
        Assert.NotNull(WaitForElement(window, "MainTabControl"));
        Assert.NotEmpty(WaitForRows(window, "CatalogListView"));
        Assert.False(WaitForElement(window, "CancelBtn").IsEnabled);
    }

    [Fact]
    public void ProfileChange_UpdatesCatalogContent()
    {
        var window = LaunchAndGetMainWindow();
        var initialCount = WaitForRows(window, "CatalogListView").Length;

        WaitForElement(window, "Profiledev").Click();
        WaitUntil(() => WaitForRows(window, "CatalogListView", throwOnTimeout: false).Length > initialCount);

        var rows = WaitForRows(window, "CatalogListView");
        Assert.True(rows.Length > initialCount, $"Expected more than {initialCount} rows after selecting Dev profile, got {rows.Length}.");
    }

    [Fact]
    public void ThemeChange_And_SearchByEnter_ShowResults()
    {
        var window = LaunchAndGetMainWindow();
        var comboBox = WaitForElement(window, "ThemeComboBox").AsComboBox();

        comboBox.Select(1);
        WaitUntil(() => string.Equals(comboBox.SelectedItem?.Text, "Neon Cyberpunk", StringComparison.Ordinal));

        SelectTab(window, "SearchTab");
        var searchBox = WaitForElement(window, "SearchTextBox").AsTextBox();
        searchBox.Enter("Git");
        searchBox.Focus();
        Keyboard.Type(VirtualKeyShort.RETURN);

        WaitUntil(() => WaitForRows(window, "SearchResultsListView", throwOnTimeout: false).Length > 0);

        Assert.NotEmpty(WaitForRows(window, "SearchResultsListView"));
    }

    [Fact]
    public void DebloaterTab_ShowsDetectedPackages_AndExpectedActionStates()
    {
        var window = LaunchAndGetMainWindow();

        SelectTab(window, "DebloaterTab");
        var rows = WaitForRows(window, "BloatwareListView");

        Assert.NotEmpty(rows);
        Assert.True(WaitForElement(window, "ScanBloatwareBtn").IsEnabled);
        Assert.True(WaitForElement(window, "RemoveBloatwareBtn").IsEnabled);
        Assert.False(WaitForElement(window, "CancelDebloatBtn").IsEnabled);
    }

    private Window LaunchAndGetMainWindow(int timeoutSeconds = 20)
    {
        Assert.True(File.Exists(ExePath), $"Initio.exe not found at: {ExePath}");

        var startInfo = new ProcessStartInfo(ExePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(ExePath)!
        };
        startInfo.Environment["INITIO_TEST_MODE"] = "1";

        _app = Application.Launch(startInfo);
        var window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(timeoutSeconds));
        Assert.NotNull(window);
        window.Focus();
        Thread.Sleep(1500);
        _output.WriteLine($"Launched window: {window.Title}");
        return window;
    }

    private void SelectTab(Window window, string automationId)
    {
        var tab = WaitForElement(window, automationId).AsTabItem();
        tab.Select();
        Thread.Sleep(300);
    }

    private AutomationElement WaitForElement(AutomationElement parent, string automationId, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var element = parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        _output.WriteLine($"TIMEOUT: Could not find AutomationId='{automationId}' under '{parent.Name}'.");
        throw new Xunit.Sdk.XunitException($"Element with AutomationId '{automationId}' not found.");
    }

    private AutomationElement[] WaitForRows(AutomationElement parent, string automationId, int timeoutMs = 5000, bool throwOnTimeout = true)
    {
        var container = WaitForElement(parent, automationId, timeoutMs);
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var rows = GetRowLikeElements(container);
            if (rows.Length > 0)
            {
                return rows;
            }

            Thread.Sleep(100);
        }

        if (!throwOnTimeout)
        {
            return [];
        }

        throw new Xunit.Sdk.XunitException($"No row elements found in '{automationId}'.");
    }

    private static AutomationElement[] GetRowLikeElements(AutomationElement container)
    {
        return container.FindAllDescendants()
            .Where(element => element.ControlType == ControlType.DataItem || element.ControlType == ControlType.ListItem)
            .ToArray();
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.True(condition(), "Timed out waiting for condition.");
    }
}
