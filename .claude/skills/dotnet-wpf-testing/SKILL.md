---
name: dotnet-wpf-testing
description: WPF-specific testing guidance extending the global /dotnet-testing skill. Covers XAML resource validation, theme consistency tests, build-first DLL reference pattern, and FlaUI UI test scaffolding. Use for Initio-specific test creation.
---

# WPF Testing for Initio

Extends the global `/dotnet-testing` skill with project-specific patterns.

## Critical: Build-First Pattern

Tests reference the main project DLL directly (not a project reference):

```xml
<Reference Include="Initio">
  <HintPath>..\bin\Debug\net8.0-windows\win-x64\Initio.dll</HintPath>
</Reference>
```

**Always build before testing:**
```bash
dotnet build NewPCSetupWPF.csproj && dotnet test Tests/Initio.Tests.csproj
```

Never run `dotnet test` alone — it will fail with missing DLL errors.

## Test File Location

All tests go in `Tests/CatalogServiceTests.cs` (single test file).

## Test Naming Convention

```
Method_Scenario_ExpectedResult
```

Group related tests with section separators:
```csharp
// ═══ Section Name ═══
```

## WPF-Specific Test Patterns

### Model Property Change Notification
```csharp
[Fact]
public void AppItem_SetProperty_RaisesPropertyChanged()
{
    var item = new AppItem { Id = "Test.App", Name = "Test", Category = "Utilities" };
    var raised = false;
    item.PropertyChanged += (_, e) => { if (e.PropertyName == "IsSelected") raised = true; };
    item.IsSelected = true;
    Assert.True(raised);
}
```

### IsInstalled Clears IsSelected (Business Rule)
```csharp
[Fact]
public void AppItem_SetInstalled_ClearsSelection()
{
    var item = new AppItem { Id = "Test.App", Name = "Test", Category = "Utilities", IsSelected = true };
    item.IsInstalled = true;
    Assert.False(item.IsSelected);
}
```

### Catalog Service Loading
```csharp
[Fact]
public void LoadCatalog_ReturnsNonEmptyList()
{
    var apps = CatalogService.LoadCatalogFromEmbeddedResource();
    Assert.NotEmpty(apps);
    Assert.All(apps, a => Assert.False(string.IsNullOrEmpty(a.Name)));
}
```

### Theme Resource Validation (test all themes have same keys)
```csharp
[Fact]
public void AllThemes_HaveSameResourceKeys()
{
    var themesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Themes");
    var themeFiles = Directory.GetFiles(themesDir, "Theme.*.xaml");
    Assert.Equal(5, themeFiles.Length);

    var keysByTheme = themeFiles.ToDictionary(
        f => Path.GetFileName(f),
        f => Regex.Matches(File.ReadAllText(f), @"x:Key=""([^""]+)""")
               .Select(m => m.Groups[1].Value)
               .OrderBy(k => k)
               .ToList()
    );

    var reference = keysByTheme.First();
    foreach (var (theme, keys) in keysByTheme.Skip(1))
    {
        Assert.True(reference.Value.SequenceEqual(keys),
            $"{theme} has different ResourceKeys than {reference.Key}");
    }
}
```

## UI Tests (FlaUI)

UI tests are in `Tests.UI/InitioAppTests.cs` (separate project). They:
- Launch the published .exe
- Use FlaUI to interact with WPF automation
- Require a published build first

```bash
dotnet publish NewPCSetupWPF.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet test Tests.UI/Initio.Tests.UI.csproj
```

## Verification Workflow

After writing tests:
1. `dotnet build NewPCSetupWPF.csproj` — 0 errors
2. `dotnet build && dotnet test Tests/Initio.Tests.csproj` — all pass
3. If UI tests exist: publish first, then run UI tests
