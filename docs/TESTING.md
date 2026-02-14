# Testing

## Overview

| Aspect | Details |
|--------|---------|
| Unit test framework | xUnit |
| UI test framework | FlaUI (Windows UI Automation) |
| Test location | `Tests/` (unit), `Tests.UI/` (UI automation) |

## Running Tests

| Command | Description |
|---------|-------------|
| `dotnet test` | Run all tests |
| `dotnet test --filter "FullyQualifiedName~CatalogServiceTests"` | Run unit tests only |
| `dotnet test --filter "FullyQualifiedName~InitioAppTests"` | Run UI tests only |

## Test Structure

### Unit Tests (`Tests/CatalogServiceTests.cs`)

Tests for `CatalogService` and model behavior:

- **Embedded catalog loading**: Verifies the embedded JSON resource loads correctly
- **JSON parsing**: Validates category counts, app properties, wingetId format
- **StoreTrendItem properties**: Tests `TrendScore` computation, `Rating` calculation, `PopularitySignal` assignment
- **Property change notifications**: Verifies `INotifyPropertyChanged` fires for `IsSelected` and `CatalogStatus`
- **Async load flow**: Tests the remote -> cache -> embedded fallback chain

### UI Tests (`Tests.UI/InitioAppTests.cs`)

End-to-end tests using FlaUI to automate the running application:

- **Window launch**: Verifies the app starts and the main window is visible
- **Tab navigation**: Tests switching between My Setup, Store, and Search tabs
- **Catalog population**: Verifies the ListView populates with app items on startup
- **Element discovery**: Finds controls by AutomationId (MainTabControl, CatalogListView)

> UI tests require the application to be built first. They launch the compiled `.exe` and interact with it via Windows UI Automation.

## Writing Tests

### Unit test pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var items = CatalogService.LoadEmbeddedCatalog();

    // Act
    var result = items.First();

    // Assert
    Assert.NotEmpty(result.Name);
    Assert.Contains(".", result.WingetId);
}
```

### UI test pattern

```csharp
[Fact]
public void UIElement_Action_ExpectedState()
{
    using var app = Application.Launch("path/to/Initio.exe");
    var window = app.GetMainWindow(Automation);

    var tab = window.FindFirstDescendant(cf => cf.ByAutomationId("MainTabControl"));
    Assert.NotNull(tab);
}
```
