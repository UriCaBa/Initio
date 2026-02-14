# Architecture

## Overview

Initio is a single-window WPF desktop application following an MVVM-inspired pattern with code-behind. The application is structured as a monolith with clear separation between UI, models, and services via partial classes and dedicated service layer.

## System Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                        MainWindow                            │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │  Sidebar     │  │  Tab Control │  │  Installation      │  │
│  │  - Themes    │  │  - My Setup  │  │  Engine             │  │
│  │  - Profiles  │  │  - Store     │  │  (Install.cs)      │  │
│  │  - Actions   │  │  - Search    │  │                    │  │
│  │  - Logs      │  │              │  │                    │  │
│  └─────────────┘  └──────┬───────┘  └────────┬───────────┘  │
│                          │                    │              │
│         ┌────────────────┴────────────────────┘              │
│         ▼                                                    │
│  ┌─────────────────────────────────────────────────────┐     │
│  │              ObservableCollections                   │     │
│  │  _appItems  |  _storeTrendItems  |  _searchResults  │     │
│  └──────┬──────────────┬──────────────────┬────────────┘     │
└─────────┼──────────────┼──────────────────┼──────────────────┘
          │              │                  │
          ▼              ▼                  ▼
   ┌────────────┐ ┌──────────────┐  ┌──────────────────┐
   │  AppItem   │ │StoreTrendItem│  │  WingetSearch     │
   │  (Model)   │ │  (Model)     │  │  Service          │
   └────────────┘ └──────────────┘  └──────────────────┘
                         ▲
                         │
                  ┌──────────────┐
                  │ CatalogService│
                  │ Remote→Cache  │
                  │ →Embedded     │
                  └──────────────┘
```

## Directory Structure

| Directory | Purpose |
|-----------|---------|
| `/` (root) | Application entry point (`App.xaml`), main window, project config |
| `Models/` | Data models with `INotifyPropertyChanged` for WPF binding |
| `Services/` | Business logic — catalog loading, winget process interaction |
| `Converters/` | WPF value converters for XAML data binding |
| `Themes/` | XAML ResourceDictionaries defining color schemes and styles |
| `Images/` | Application icons and visual assets |
| `Tests/` | xUnit unit tests |
| `Tests.UI/` | FlaUI-based UI automation tests |

## Key Components

### MainWindow (UI + ViewModel)
- **Location**: `MainWindow.xaml`, `MainWindow.xaml.cs`
- **Purpose**: Single application window — acts as both View and ViewModel
- **Key responsibilities**:
  - Theme switching via ResourceDictionary swapping
  - Profile management (Default, Dev, Home Office, Gaming, Custom)
  - Catalog filtering/sorting via `ICollectionView`
  - Store browsing with dynamic category tabs
  - Winget search integration
  - Status bar, progress tracking, and log display
- **Pattern**: Implements `INotifyPropertyChanged` directly on the Window class

### Installation Engine
- **Location**: `MainWindow.Install.cs` (partial class)
- **Purpose**: All winget installation logic, separated from UI management
- **Key responsibilities**:
  - Winget availability detection (`winget --version`)
  - Installed app state refresh (`winget list`)
  - Sequential batch installation with retry (max 2 retries per app)
  - Per-app timeout (15 minutes), cancellation support
  - ETA calculation, progress reporting, live logging
  - Process management with proper cleanup

### AppItem Model
- **Location**: `Models/AppItem.cs`
- **Purpose**: Represents an app in the user's "My Setup" catalog
- **Key properties**: `Name`, `Category`, `WingetId` (immutable), `IsSelected`, `IsInstalled`, `InstallStatus` (bindable)
- **Behavior**: Setting `IsInstalled = true` automatically unchecks `IsSelected`

### StoreTrendItem Model
- **Location**: `Models/StoreTrendItem.cs`
- **Purpose**: Represents an app from the remote store catalog or winget search
- **Key properties**: `Category`, `Rank`, `Name`, `WingetId`, `Rating`, `PopularitySignal`, `TrendScore` (computed), `IsSelected`, `CatalogStatus`
- **Scoring**: `TrendScore = max(42, 100 - (rank - 1) * 3)`, `Rating = max(3.5, round(4.9 - (rank - 1) * 0.04, 1))`

### CatalogService
- **Location**: `Services/CatalogService.cs`
- **Purpose**: Loads the app catalog with three-tier fallback
- **Strategy**: Remote GitHub JSON (5s timeout) -> Local cache (`%APPDATA%/Initio/catalog_cache.json`) -> Embedded resource (compiled into .exe)
- **Key method**: `LoadAsync()` returns `(IReadOnlyList<StoreTrendItem>, string source)`

### WingetSearchService
- **Location**: `Services/WingetSearchService.cs`
- **Purpose**: Async winget repository search with robust output parsing
- **Key method**: `SearchAsync(query, maxResults, cancellationToken)` — runs winget on background thread with 15s timeout
- **Parsing**: Column-position detection from separator line, fallback to whitespace splitting

## Data Flow

### Application Startup
1. `App.xaml.cs` — registers global exception handlers
2. `MainWindow()` constructor — loads embedded catalog instantly, populates default 12 apps, initializes themes/profiles/views
3. `Window_Loaded` — checks winget availability, runs `winget list` to refresh installed states
4. `LoadCatalogAsync()` — background task attempts remote catalog, falls back to cache or embedded

### Installation Flow
1. User selects apps (via profile or manual checkboxes)
2. Click "Install Selected" or "Install All"
3. `InstallAppsAsync()` iterates selected apps sequentially
4. Per app: `winget install --id {WingetId} [--silent] --accept-package-agreements --accept-source-agreements`
5. Success detection: looks for "Successfully installed", "Already installed", "No available upgrade"
6. Fallback verification: `winget list "{query}"` to confirm installation
7. Retry up to 2 times on failure
8. UI updates: progress bar, ETA, status text, log entries

### Catalog Fallback Chain
```
Remote (GitHub raw)  ──[5s timeout]──> Cache (%APPDATA%)  ──[read fail]──> Embedded (.exe resource)
        │                                    ▲
        └──── SaveCacheAsync() ──────────────┘
```

## Design Decisions

- **Partial classes**: `MainWindow.cs` and `MainWindow.Install.cs` split UI management from installation logic, keeping each file focused
- **Code-behind over full MVVM**: Simplified architecture for a single-window app — no ViewModel layer, no DI container, no command framework
- **Static services**: `CatalogService` and `WingetSearchService` are static classes — appropriate for stateless operations with no instance dependencies
- **Embedded catalog**: `catalog.json` compiled as embedded resource ensures the app works offline on first launch without any network access
- **Process-based winget integration**: Uses `System.Diagnostics.Process` to invoke winget CLI directly, parsing stdout — avoids dependency on winget COM APIs or NuGet packages
- **No external NuGet dependencies**: The application uses only standard .NET 8 libraries, minimizing supply chain risk and simplifying distribution

## Theme System

Five themes implemented as XAML ResourceDictionaries, each defining a consistent color palette:

| Theme | File | Style |
|-------|------|-------|
| Midnight Blue | `Theme.DarkElegant.xaml` | Dark blue with soft gold accents |
| Neon Cyberpunk | `Theme.GamerRgb.xaml` | Dark with neon accent colors |
| Slate Professional | `Theme.Corporate.xaml` | Neutral business tones |
| Gemini AI | `Theme.Gemini.xaml` | AI-inspired branding |
| Hacker Terminal | `Theme.Hacker.xaml` | Green-on-black terminal aesthetic |

Themes are swapped at runtime by replacing the merged ResourceDictionary in `Application.Current.Resources`. All UI elements use `DynamicResource` bindings to pick up theme changes immediately.
