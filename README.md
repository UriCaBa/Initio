# Initio

Initio is a WPF desktop app for provisioning a fresh Windows machine with `winget`. The app now uses a split architecture: a WPF shell for windowing and presentation, and a reusable `Initio.Core` project for catalog, install, search, debloat, viewmodels, and command/state management.

## Overview

Initio lets you start from a curated setup, switch profiles, browse a store-like catalog, search `winget`, and run batch installs with progress, ETA, retries, cancellation, and in-app logs. It also includes a debloat flow for common preinstalled packages and a test mode that boots the app with fake services and no external process or network calls.

## Tech Stack

- Runtime: .NET 8
- UI: WPF (`net8.0-windows`)
- Core library: `Initio.Core` (`net8.0`)
- Language: C# 12 with nullable enabled
- Catalog format: JSON (`catalog.json` embedded + remote + cache)
- Installer backend: `winget` CLI
- Tests: xUnit + FlaUI
- Distribution: self-contained single-file `win-x64` executable

## Quick Start

```powershell
dotnet build NewPCSetupWPF.csproj
dotnet run --project NewPCSetupWPF.csproj
```

### Test Mode

Use test mode when you want deterministic UI behavior without calling `winget`, PowerShell debloat commands, or the network.

```powershell
$env:INITIO_TEST_MODE = '1'
dotnet run --project NewPCSetupWPF.csproj
```

## Build and Test

```powershell
dotnet build NewPCSetupWPF.csproj
dotnet test Tests\Initio.Tests.csproj
dotnet test Tests.UI\Initio.UITests.csproj
```

## Executable Outputs

- Debug build: `bin\Debug\net8.0-windows\win-x64\Initio.exe`
- Publish output: `bin\Release\net8.0-windows\win-x64\publish\Initio.exe`

## Project Structure

```text
Initio/
|- App.xaml / App.xaml.cs                 # App startup and crash logging
|- MainWindow.xaml / MainWindow.xaml.cs   # WPF shell and window chrome
|- Controls/                              # Sidebar and per-tab user controls
|- Services/                              # WPF-side composition, process runner, fake services
|- Themes/                                # Theme dictionaries and shared styles
|- Initio.Core/                           # Models, abstractions, services, viewmodels, commands
|- Tests/                                 # Unit tests for core logic
|- Tests.UI/                              # FlaUI UI automation tests
|- docs/                                  # Project docs
|- call/                                  # Architecture bundle for repo walkthroughs
|- catalog.json                           # Embedded catalog source
|- NewPCSetupWPF.csproj                   # WPF application project
|- NewPCSetupWPF.sln                      # Solution with app, core, unit tests, UI tests
```

## Key Features

- Profiles: Default, Dev, Office, Gaming, Custom
- Catalog fallback: remote -> cache -> embedded
- Batch install engine: 2 retries, 15 minute timeout per app, cancellation, ETA, logs
- Store and `winget` search flows that can add apps into "My Setup"
- Debloat scanning/removal for known package list
- Five runtime-switchable themes
- Compact layout mode below 1220 px width
- `INITIO_TEST_MODE=1` for stable automated tests

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Setup](docs/SETUP.md)
- [Testing](docs/TESTING.md)
- [Deployment](docs/DEPLOYMENT.md)
- [Call docs bundle](call/README.md)

## License

MIT. See [LICENSE](LICENSE).
