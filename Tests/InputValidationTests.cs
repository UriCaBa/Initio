using NewPCSetupWPF.Services;

namespace Initio.Tests;

public class InputValidationTests
{
    // ═══ IsValidWingetId ═══

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

    // ═══ SanitizeSearchQuery ═══

    [Fact]
    public void SanitizeSearchQuery_RemovesDangerousCharacters()
    {
        var result = InputValidation.SanitizeSearchQuery("chrome;echo pwned&rm -rf|cat$(`test`)\"bad\"");

        Assert.DoesNotContain(";", result);
        Assert.DoesNotContain("&", result);
        Assert.DoesNotContain("|", result);
        Assert.DoesNotContain("$", result);
        Assert.DoesNotContain("(", result);
        Assert.DoesNotContain(")", result);
        Assert.DoesNotContain("`", result);
        Assert.DoesNotContain("\"", result);
    }

    [Fact]
    public void SanitizeSearchQuery_PreservesNormalText()
    {
        var result = InputValidation.SanitizeSearchQuery("Visual Studio Code");
        Assert.Equal("Visual Studio Code", result);
    }

    [Fact]
    public void SanitizeSearchQuery_TrimsResult()
    {
        var result = InputValidation.SanitizeSearchQuery("  chrome  ");
        Assert.Equal("chrome", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeSearchQuery_EmptyInput_ReturnsEmpty(string? input)
    {
        var result = InputValidation.SanitizeSearchQuery(input);
        Assert.Equal(string.Empty, result);
    }

    // ═══ IsValidPackageName ═══

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
