using Initio.Core.ViewModels;

namespace Initio.Tests;

public class DebloatViewModelTests
{
    [Fact]
    public void SetItems_LeavesRowsInNotScannedStateUntilDetectionRuns()
    {
        var viewModel = new DebloatViewModel();

        viewModel.SetItems(TestData.CreateBloatwareItems());

        Assert.NotEmpty(viewModel.AllItems);
        Assert.All(viewModel.AllItems, item => Assert.Equal("Not Scanned", item.RemovalStatus));
        Assert.Contains("0 detected", viewModel.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
