# Initio

A modern WPF desktop application for quickly bootstrapping a fresh Windows PC by batch-installing apps via **winget** (Windows Package Manager).

## Overview

Initio lets users pick from curated app catalogs or search the winget repository, then batch-install everything in one click. It ships as a single portable `.exe` with an embedded catalog of ~200 apps across 7 categories, 5 switchable themes, and quick-setup profiles for common use cases (Dev, Home Office, Gaming).

## Tech Stack

- **Runtime**: .NET 8.0
- **Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# 12 (nullable enabled)
- **Data**: JSON (System.Text.Json) — embedded + remote catalog
- **Package Manager**: winget (Windows Package Manager)
- **Testing**: xUnit + FlaUI (UI automation)
- **Deployment**: Single-file, self-contained executable (win-x64)

## Quick Start

```powershell
# Clone
git clone https://github.com/UriCaBa/Initio.git
cd Initio

# Run
dotnet run
```

> Requires .NET 8 SDK and winget installed. See [Setup Guide](docs/SETUP.md) for details.

## Features

- **Quick Profiles**: One-click app selection for Dev, Home Office, Gaming, or Custom setups
- **Curated Catalog**: ~200 apps across 7 categories (Productivity, Communication, Media, Development, Gaming, Security, Utilities)
- **Store Browser**: Browse trending apps by category with trend scores and ratings
- **Live Winget Search**: Search the full winget repository in real time
- **Batch Install**: Sequential installation with retry logic (2 retries), per-app timeouts (15 min), ETA, and live logs
- **5 Themes**: Midnight Blue, Neon Cyberpunk, Slate Professional, Gemini AI, Hacker Terminal — switchable at runtime
- **Offline Support**: Three-tier catalog fallback (remote GitHub -> local cache -> embedded JSON)
- **Portable**: Ships as a single `.exe` with no installation required

## Project Structure

```
NewPCSetupWPF/
├── App.xaml / .cs                  # Application entry point, global error handling
├── MainWindow.xaml / .cs           # Main UI: layout, themes, profiles, catalog management
├── MainWindow.Install.cs           # Installation engine (partial class)
├── Models/
│   ├── AppItem.cs                  # "My Setup" app model with install status
│   └── StoreTrendItem.cs           # Store/search app model with trend scoring
├── Services/
│   ├── CatalogService.cs           # Catalog loader (remote/cache/embedded fallback)
│   └── WingetSearchService.cs      # Async winget search with output parsing
├── Converters/
│   └── InverseBoolConverter.cs     # Bool inversion for XAML bindings
├── Themes/                         # 5 theme ResourceDictionaries
│   ├── Theme.DarkElegant.xaml
│   ├── Theme.GamerRgb.xaml
│   ├── Theme.Corporate.xaml
│   ├── Theme.Gemini.xaml
│   └── Theme.Hacker.xaml
├── Images/                         # App icons (ico, png)
├── Tests/
│   └── CatalogServiceTests.cs      # Unit tests for catalog loading and models
├── Tests.UI/
│   └── InitioAppTests.cs           # UI automation tests (FlaUI)
├── catalog.json                    # Embedded app catalog (~200 apps, 7 categories)
└── NewPCSetupWPF.csproj            # Project configuration
```

## Available Commands

| Command | Description |
|---------|-------------|
| `dotnet run` | Run in development mode |
| `dotnet build` | Build the project |
| `dotnet test` | Run unit tests |
| `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` | Publish portable executable |

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — system design, components, data flow
- [Setup Guide](docs/SETUP.md) — prerequisites, installation, environment
- [Testing](docs/TESTING.md) — test framework, running tests, structure
- [Deployment](docs/DEPLOYMENT.md) — building, publishing, distribution

## License

MIT License — see [LICENSE](LICENSE) for details.
