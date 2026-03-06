# Testing

## Test Projects

| Project | Scope | Notes |
|---|---|---|
| `Tests/Initio.Tests.csproj` | Unit and viewmodel tests for `Initio.Core` | No dependency on the built WPF executable |
| `Tests.UI/Initio.UITests.csproj` | FlaUI end-to-end tests against the compiled app | Launches the real `.exe` with `INITIO_TEST_MODE=1` |

## Commands

Run core tests:

```powershell
dotnet test Tests\Initio.Tests.csproj
```

Run UI automation tests:

```powershell
dotnet build NewPCSetupWPF.csproj
dotnet test Tests.UI\Initio.UITests.csproj
```

Run a single UI test class:

```powershell
dotnet test Tests.UI\Initio.UITests.csproj --filter "FullyQualifiedName~InitioAppTests"
```

## What Is Covered

### Core and unit coverage

The unit suite covers:
- `InputValidation` for `wingetId`, package names, and search sanitization
- `CatalogService` remote/cache/embedded fallback
- `WingetClient` parsing and argument sanitization
- `BloatwareService` known package list, detect/remove/verify behavior
- `MainViewModel` initialization, profile switching, compact layout, selection summaries
- install retry and cancel behavior
- debloat command behavior and summaries

See:
- `Tests/InputValidationTests.cs`
- `Tests/CatalogServiceTests.cs`
- `Tests/WingetClientTests.cs`
- `Tests/BloatwareServiceTests.cs`
- `Tests/MainViewModelTests.cs`

### UI coverage

The UI suite covers the shell in deterministic mode:
- app launch and shell render
- profile switching
- theme change
- search via Enter key
- Debloater tab visibility and action state

See:
- `Tests.UI/InitioAppTests.cs`

## Test Mode

UI tests rely on:

```powershell
$env:INITIO_TEST_MODE = '1'
```

In this mode the app:
- does not call real `winget`
- does not call `Get-AppxPackage`
- does not hit the network
- returns deterministic fake catalog, search, and debloat data

The same mode is useful for manual smoke testing when you want to validate layout, bindings, and automation IDs without touching the host machine.

## Running the App for Manual QA in Test Mode

```powershell
$env:INITIO_TEST_MODE = '1'
dotnet run --project NewPCSetupWPF.csproj
```

## Automation IDs

The UI tests depend on stable `AutomationId` values, including:
- `ThemeComboBox`
- `MainTabControl`
- `CatalogListView`
- `SearchTextBox`
- `SearchResultsListView`
- `BloatwareListView`
- `InstallBtn`
- `CancelBtn`
- `ScanBloatwareBtn`
- `RemoveBloatwareBtn`

If you change these IDs, update the FlaUI tests in the same change.

## Notes and Constraints

- UI tests require an interactive Windows desktop session.
- The UI suite launches the built executable from `bin\Debug\net8.0-windows\win-x64\Initio.exe`.
- Unit tests target `Initio.Core` directly, which removes the old fragile dependency on a built WPF DLL.
