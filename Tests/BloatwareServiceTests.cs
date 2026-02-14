using NewPCSetupWPF.Models;
using NewPCSetupWPF.Services;

namespace Initio.Tests;

public class BloatwareServiceTests
{
    // ═══ Known Bloatware List ═══

    [Fact]
    public void GetKnownBloatware_ReturnsNonEmptyList()
    {
        var items = BloatwareService.GetKnownBloatware();

        Assert.NotNull(items);
        Assert.NotEmpty(items);
    }

    [Fact]
    public void GetKnownBloatware_HasExpectedCategories()
    {
        var items = BloatwareService.GetKnownBloatware();
        var categories = items.Select(i => i.Category).Distinct().ToList();

        Assert.Contains("Games", categories);
        Assert.Contains("Social & Entertainment", categories);
        Assert.Contains("Microsoft Bloat", categories);
        Assert.Contains("Promotions", categories);
        Assert.Equal(4, categories.Count);
    }

    [Fact]
    public void GetKnownBloatware_EachItemHasValidProperties()
    {
        var items = BloatwareService.GetKnownBloatware();

        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"Item has empty Name");
            Assert.False(string.IsNullOrWhiteSpace(item.Category), $"Item has empty Category");
            Assert.False(string.IsNullOrWhiteSpace(item.PackageName), $"Item has empty PackageName");
            Assert.False(string.IsNullOrWhiteSpace(item.Description), $"Item has empty Description");
        }
    }

    [Fact]
    public void GetKnownBloatware_NoDuplicatePackageNames()
    {
        var items = BloatwareService.GetKnownBloatware();
        var packageNames = items.Select(i => i.PackageName).ToList();

        Assert.Equal(packageNames.Count, packageNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void GetKnownBloatware_HasMinimumCount()
    {
        var items = BloatwareService.GetKnownBloatware();

        Assert.True(items.Count >= 30, $"Expected at least 30 bloatware items, got {items.Count}");
    }

    // ═══ BloatwareItem Model ═══

    [Fact]
    public void BloatwareItem_DefaultState_IsCorrect()
    {
        var item = new BloatwareItem
        {
            Name = "Test", Category = "Test", PackageName = "test.pkg", Description = "Test"
        };

        Assert.False(item.IsSelected);
        Assert.False(item.IsInstalled);
        Assert.Equal("Detected", item.RemovalStatus);
    }

    [Fact]
    public void BloatwareItem_SetInstalledFalse_DeselectsItem()
    {
        var item = new BloatwareItem
        {
            Name = "Test", Category = "Test", PackageName = "test.pkg", Description = "Test"
        };
        item.IsInstalled = true;
        item.IsSelected = true;

        item.IsInstalled = false;

        Assert.False(item.IsSelected);
    }

    [Fact]
    public void BloatwareItem_PropertyChanged_FiresForIsSelected()
    {
        var item = new BloatwareItem
        {
            Name = "Test", Category = "Test", PackageName = "test.pkg", Description = "Test"
        };
        var fired = false;
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BloatwareItem.IsSelected)) fired = true;
        };

        item.IsSelected = true;

        Assert.True(fired);
    }

    [Fact]
    public void BloatwareItem_PropertyChanged_FiresForRemovalStatus()
    {
        var item = new BloatwareItem
        {
            Name = "Test", Category = "Test", PackageName = "test.pkg", Description = "Test"
        };
        var fired = false;
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BloatwareItem.RemovalStatus)) fired = true;
        };

        item.RemovalStatus = "Removed";

        Assert.True(fired);
    }
}
