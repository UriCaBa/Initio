using Initio.Core.Abstractions;
using Initio.Core.Services;

namespace Initio.Tests;

public class WingetClientTests
{
    [Fact]
    public void ParseSearchResults_ParsesColumnAlignedOutput()
    {
        const string output = """
Name                         Id                            Version Source
--------------------------------------------------------------------------
Git                          Git.Git                       2.47.1  winget
Visual Studio Code           Microsoft.VisualStudioCode    1.98.0  winget
""";

        var results = WingetClient.ParseSearchResults(output, 10);

        Assert.Collection(results,
            first =>
            {
                Assert.Equal("Git", first.Name);
                Assert.Equal("Git.Git", first.WingetId);
            },
            second =>
            {
                Assert.Equal("Visual Studio Code", second.Name);
                Assert.Equal("Microsoft.VisualStudioCode", second.WingetId);
            });
    }

    [Fact]
    public void ParseSearchResults_UsesSplitFallbackWhenRowsHaveLooseSpacing()
    {
        const string output = """
PowerToys              Microsoft.PowerToys     0.89.0
OBS Studio             OBSProject.OBSStudio    31.0.0
""";

        var results = WingetClient.ParseSearchResults(output, 10);

        Assert.Collection(results,
            first => Assert.Equal("Microsoft.PowerToys", first.WingetId),
            second => Assert.Equal("OBSProject.OBSStudio", second.WingetId));
    }

    [Fact]
    public async Task SearchAsync_SanitizesQuery_RespectsMaxResults_AndUsesTrustedPath()
    {
        const string output = """
Name                         Id                            Version Source
--------------------------------------------------------------------------
Git                          Git.Git                       2.47.1  winget
GitHub Desktop               GitHub.GitHubDesktop         3.4.18  winget
GitKraken                    Axosoft.GitKraken            10.8.0  winget
""";
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(new ProcessResult(0, output, string.Empty));
        var wingetPath = CreateFakeExecutablePath("winget.exe");
        var client = new WingetClient(runner, wingetPath);

        var results = await client.SearchAsync("git;rm -rf", 2);

        Assert.Equal(2, results.Count);
        Assert.Single(runner.Calls);
        Assert.Equal(wingetPath, runner.Calls[0].FileName);
        Assert.DoesNotContain(";", runner.Calls[0].Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("$", runner.Calls[0].Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsNullWhenTrustedWingetPathCannotBeResolved()
    {
        var runner = new RecordingProcessRunner();
        var client = new WingetClient(runner, @"C:\missing\winget.exe");

        var result = await client.GetVersionAsync();

        Assert.Null(result);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsNullWhenWingetPathIsRelative()
    {
        var runner = new RecordingProcessRunner();
        var client = new WingetClient(runner, "winget.exe");

        var result = await client.GetVersionAsync();

        Assert.Null(result);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task InstallAsync_InvalidWingetId_ReturnsNullAndSkipsProcessRunner()
    {
        var runner = new RecordingProcessRunner();
        var client = new WingetClient(runner, CreateFakeExecutablePath("winget.exe"));

        var result = await client.InstallAsync("bad id", silent: true, timeout: TimeSpan.FromSeconds(5));

        Assert.Null(result);
        Assert.Empty(runner.Calls);
    }

    private static string CreateFakeExecutablePath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, fileName);
        File.WriteAllText(fullPath, "stub");
        return fullPath;
    }
}
