# Development Setup

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 or 11 | WPF build and UI automation are Windows-only |
| .NET 8 SDK | Required to build and run the solution |
| `winget` | Required for real install flows |

## Restore

```powershell
dotnet restore NewPCSetupWPF.csproj
```

If you want all test projects restored as well:

```powershell
dotnet restore Tests\Initio.Tests.csproj
dotnet restore Tests.UI\Initio.UITests.csproj
```

## Run Locally

Normal mode:

```powershell
dotnet run --project NewPCSetupWPF.csproj
```

Deterministic test mode:

```powershell
$env:INITIO_TEST_MODE = '1'
dotnet run --project NewPCSetupWPF.csproj
```

## Build Outputs

Debug build:
- `bin\Debug\net8.0-windows\win-x64\Initio.exe`

Release publish:
- `bin\Release\net8.0-windows\win-x64\publish\Initio.exe`

Build command:

```powershell
dotnet build NewPCSetupWPF.csproj
```

## Solution Layout

- `NewPCSetupWPF.csproj`: WPF shell
- `Initio.Core/Initio.Core.csproj`: core logic and viewmodels
- `Tests/Initio.Tests.csproj`: unit tests
- `Tests.UI/Initio.UITests.csproj`: FlaUI UI tests

## Configuration

### Catalog

Catalog source order:
1. remote GitHub JSON
2. `%APPDATA%\Initio\catalog_cache.json`
3. embedded `catalog.json`

To update the catalog, edit `catalog.json` in the repo root.

### Test Mode

`INITIO_TEST_MODE=1` switches the app to fake services defined in `Services/`.

Use it when:
- running UI tests
- verifying layout or bindings offline
- avoiding process execution on a development machine

### Themes

Theme dictionaries live in `Themes/Theme.*.xaml`.
Shared styles live in `Themes/CommonStyles.xaml`.

If you add a theme, update the `ThemeOptions` list in `Initio.Core/ViewModels/MainViewModel.cs`.

## Common Issues

| Issue | What to check |
|---|---|
| `winget` unavailable | Install or repair App Installer from Microsoft Store |
| App launches but install buttons stay disabled | The app did not detect `winget`; try test mode to validate UI only |
| UI tests fail on CI or headless session | FlaUI requires an interactive desktop |
| Catalog updates do not appear | Clear `%APPDATA%\Initio\catalog_cache.json` or change the embedded `catalog.json` |
