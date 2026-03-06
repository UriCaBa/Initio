using Initio.Core.Abstractions;
using Initio.Core.Models;
using Initio.Core.Services;

namespace Initio.Tests;

public class BloatwareServiceTests
{
    private const string TrustedPowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    [Fact]
    public void GetKnownBloatware_ReturnsExpectedCategoriesAndVolume()
    {
        var service = new BloatwareService(new RecordingProcessRunner(), TrustedPowerShellPath);

        var items = service.GetKnownBloatware();

        Assert.NotEmpty(items);
        Assert.True(items.Count >= 30, $"Expected at least 30 items, got {items.Count}");
        Assert.Contains(items.Select(item => item.Category).Distinct(), category => category == "Games");
        Assert.Contains(items.Select(item => item.Category).Distinct(), category => category == "Microsoft Bloat");
        Assert.Equal(items.Count, items.Select(item => item.PackageName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task DetectInstalledAsync_ReturnsDetectedPackageNames_WithoutMutatingDefinitions()
    {
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(new ProcessResult(0, "king.com.CandyCrushSaga\nBytedancePte.Ltd.TikTok", string.Empty));
        var service = new BloatwareService(runner, TrustedPowerShellPath);
        BloatwareDefinition[] items =
        [
            new("Candy Crush Saga", "Games", "king.com.CandyCrushSaga", "Pre-installed game"),
            new("Xbox Game Bar", "Promotions", "Microsoft.XboxGamingOverlay", "Gaming overlay")
        ];

        var installed = await service.DetectInstalledAsync(items.Select(item => item.PackageName).ToArray());

        Assert.Contains("king.com.CandyCrushSaga", installed);
        Assert.DoesNotContain("Microsoft.XboxGamingOverlay", installed);
        Assert.Single(runner.Calls);
        Assert.Equal(TrustedPowerShellPath, runner.Calls[0].FileName);
    }

    [Fact]
    public async Task DetectInstalledAsync_RelativePowerShellPath_ReturnsEmptyWithoutRunning()
    {
        var runner = new RecordingProcessRunner();
        var service = new BloatwareService(runner, "powershell.exe");

        var installed = await service.DetectInstalledAsync(["king.com.CandyCrushSaga"]);

        Assert.Empty(installed);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task RemovePackageAsync_UsesExactLookupWithoutWildcards()
    {
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(new ProcessResult(0, string.Empty, string.Empty));
        var service = new BloatwareService(runner, TrustedPowerShellPath);

        var removed = await service.RemovePackageAsync("king.com.CandyCrushSaga");

        Assert.True(removed);
        Assert.Single(runner.Calls);
        Assert.Equal(TrustedPowerShellPath, runner.Calls[0].FileName);
        Assert.Contains("Get-AppxPackage -Name 'king.com.CandyCrushSaga'", runner.Calls[0].Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("*king.com.CandyCrushSaga*", runner.Calls[0].Arguments, StringComparison.Ordinal);
        Assert.Contains("PackageFullName", runner.Calls[0].Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovePackageAsync_InvalidPackageName_ReturnsFalseWithoutRunningPowerShell()
    {
        var runner = new RecordingProcessRunner();
        var service = new BloatwareService(runner, TrustedPowerShellPath);

        var removed = await service.RemovePackageAsync("bad package name");

        Assert.False(removed);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task VerifyRemovedAsync_ReturnsTrueWhenPowerShellFindsNoPackage()
    {
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(new ProcessResult(0, string.Empty, string.Empty));
        var service = new BloatwareService(runner, TrustedPowerShellPath);

        var removed = await service.VerifyRemovedAsync("king.com.CandyCrushSaga");

        Assert.True(removed);
        Assert.Single(runner.Calls);
        Assert.Contains("Get-AppxPackage -Name 'king.com.CandyCrushSaga'", runner.Calls[0].Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("*king.com.CandyCrushSaga*", runner.Calls[0].Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyRemovedAsync_ReturnsFalseWhenProcessRunnerFails()
    {
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(null);
        var service = new BloatwareService(runner, TrustedPowerShellPath);

        var removed = await service.VerifyRemovedAsync("king.com.CandyCrushSaga");

        Assert.False(removed);
    }
}
