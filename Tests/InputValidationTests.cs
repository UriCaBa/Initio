using Initio.Core.Services;

namespace Initio.Tests;

public class InputValidationTests
{
    [Theory]
    [InlineData("Google.Chrome")]
    [InlineData("Mozilla.Firefox")]
    [InlineData("Notepad++.Notepad++")]
    [InlineData("7zip.7zip")]
    [InlineData("Microsoft.VisualStudioCode")]
    [InlineData("RiotGames.RiotClient")]
    [InlineData("A278AB0D.MarchofEmpires")]
    public void IsValidWingetId_ValidIds_ReturnsTrue(string id)
    {
        Assert.True(InputValidation.IsValidWingetId(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("id with spaces")]
    [InlineData("id;drop table")]
    [InlineData("id&echo pwned")]
    [InlineData("id|malicious")]
    [InlineData("$(evil)")]
    [InlineData("`whoami`")]
    [InlineData("ChrisAndri);TaskbarX")]
    public void IsValidWingetId_InvalidIds_ReturnsFalse(string? id)
    {
        Assert.False(InputValidation.IsValidWingetId(id));
    }

    [Fact]
    public void SanitizeSearchQuery_RemovesDangerousCharacters()
    {
        var result = InputValidation.SanitizeSearchQuery("chrome;echo pwned&rm -rf|cat$(`test`)\"bad\"");

        Assert.DoesNotContain(";", result, StringComparison.Ordinal);
        Assert.DoesNotContain("&", result, StringComparison.Ordinal);
        Assert.DoesNotContain("|", result, StringComparison.Ordinal);
        Assert.DoesNotContain("$", result, StringComparison.Ordinal);
        Assert.DoesNotContain("(", result, StringComparison.Ordinal);
        Assert.DoesNotContain(")", result, StringComparison.Ordinal);
        Assert.DoesNotContain("`", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeSearchQuery_PreservesNormalText()
    {
        Assert.Equal("Visual Studio Code", InputValidation.SanitizeSearchQuery("Visual Studio Code"));
    }

    [Fact]
    public void SanitizeSearchQuery_TrimsResult()
    {
        Assert.Equal("chrome", InputValidation.SanitizeSearchQuery("  chrome  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeSearchQuery_EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, InputValidation.SanitizeSearchQuery(input));
    }

    [Theory]
    [InlineData("Microsoft.BingNews")]
    [InlineData("king.com.CandyCrushSaga")]
    [InlineData("4DF9E0F8.Netflix")]
    [InlineData("Facebook.Instagram")]
    public void IsValidPackageName_ValidNames_ReturnsTrue(string name)
    {
        Assert.True(InputValidation.IsValidPackageName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("name with spaces")]
    [InlineData("name;inject")]
    [InlineData("name'--drop")]
    [InlineData("*wildcard*")]
    [InlineData("$(evil)")]
    public void IsValidPackageName_InvalidNames_ReturnsFalse(string? name)
    {
        Assert.False(InputValidation.IsValidPackageName(name));
    }
}
