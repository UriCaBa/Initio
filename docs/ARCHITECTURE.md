# Architecture

## Overview

Initio is a two-layer desktop application:

1. `NewPCSetupWPF` is the Windows-only shell.
2. `Initio.Core` contains the non-visual application logic.

The shell is responsible for startup, window chrome, theme resource swapping, and wiring events that are intentionally kept outside the core. The core owns state, commands, catalog loading, `winget` integration, debloat orchestration, and the viewmodels bound by the WPF UI.

## High-Level Layout

```text
App.xaml.cs
  -> AppServiceFactory
      -> MainViewModel
          -> CatalogViewModel
          -> StoreViewModel
          -> SearchViewModel
          -> DebloatViewModel
          -> ICatalogService
          -> IWingetClient
          -> IBloatwareService

MainWindow.xaml / MainWindow.xaml.cs
  -> SidebarPane
  -> CatalogTabView
  -> StoreTabView
  -> SearchTabView
  -> DebloatTabView
```

## Projects

### `NewPCSetupWPF`

Purpose:
- Starts the application
- Registers crash handlers
- Creates the `MainViewModel`
- Hosts the shell window and custom title bar
- Loads and swaps WPF theme dictionaries
- Keeps the personal `Activate Windows` button flow out of the core refactor

Key files:
- `App.xaml.cs`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Controls/*.xaml`
- `Services/AppServiceFactory.cs`
- `Services/ProcessRunner.cs`
- `Services/Fake*.cs`
- `Themes/*.xaml`

### `Initio.Core`

Purpose:
- Defines app models, services, commands, and viewmodels
- Encapsulates install, search, catalog, and debloat logic
- Exposes platform-neutral contracts so the shell can swap real and fake implementations

Key folders:
- `Abstractions/`
- `Infrastructure/`
- `Models/`
- `Services/`
- `ViewModels/`

## Service Contracts

`Initio.Core` defines four main runtime contracts:

- `ICatalogService`: loads embedded and fallback catalog data
- `IWingetClient`: wraps `winget --version`, `winget list`, `winget search`, and `winget install`
- `IBloatwareService`: detects and removes selected AppX packages
- `IProcessRunner`: low-level process execution abstraction used by Windows-only services

These interfaces make the app testable and allow `INITIO_TEST_MODE=1` to substitute fake services.

## Main ViewModel Responsibilities

`MainViewModel` is now the root state container.

It owns:
- app-wide status text, ETA, progress, and logs
- profile selection and theme selection
- install and debloat command orchestration
- layout mode (`IsCompactLayout`, `SidebarWidth`)
- synchronization between catalog, store, search, and debloat sub-viewmodels

Sub-viewmodels:
- `CatalogViewModel`: selected setup list and install status
- `StoreViewModel`: curated store list, category filtering, add-to-setup staging
- `SearchViewModel`: `winget` search results and selection state
- `DebloatViewModel`: known package list, categories, summary, selection state

## Catalog Flow

The catalog service uses a three-step fallback chain:

1. remote JSON from GitHub (`5s` timeout)
2. local cache in `%APPDATA%\Initio\catalog_cache.json`
3. embedded `catalog.json`

Parsing happens in `CatalogJsonParser`, which validates `wingetId` values before creating `StoreTrendItem` instances.

## Install Flow

The install flow lives in `MainViewModel` and uses `IWingetClient`.

Behavior kept from the previous app:
- max retries per app: `2`
- timeout per app: `15 minutes`
- cancellation via `CancellationTokenSource`
- live log accumulation in the sidebar
- ETA based on elapsed average per completed app
- verification via `winget list` after install attempts

## Debloat Flow

The debloat flow uses `IBloatwareService`.

Behavior:
- loads a known package list up front
- scans installed AppX packages
- marks `Detected`, `Not Found`, `Removing...`, `Removed`, or `Failed`
- removes selected installed items with retry/cancel support

## UI Composition

`MainWindow.xaml` is now only a host for:
- window chrome
- shell layout
- command buttons shared across tabs
- the main `TabControl`

Tab details live in:
- `Controls/CatalogTabView.xaml`
- `Controls/StoreTabView.xaml`
- `Controls/SearchTabView.xaml`
- `Controls/DebloatTabView.xaml`
- `Controls/SidebarPane.xaml`

## Themes and Styling

Themes are split into:
- per-theme dictionaries in `Themes/Theme.*.xaml`
- shared control styles in `Themes/CommonStyles.xaml`

The app swaps the active theme dictionary at runtime while keeping shared styles loaded. Main visual states, focus, caption buttons, and semantic colors are now driven from theme resources instead of local literals in `MainWindow.xaml`.

## Test Mode

When `INITIO_TEST_MODE=1` is set:
- `AppServiceFactory` creates `FakeCatalogService`
- `FakeWingetClient` returns deterministic version, installed list, search results, and install responses
- `FakeBloatwareService` returns deterministic scan and remove behavior

This mode exists to keep UI automation independent from network, `winget`, or PowerShell availability.

## Logging and Failure Handling

- `App.xaml.cs` writes unhandled crashes to `%LOCALAPPDATA%\Initio\crash_log.txt`
- `MainViewModel` accumulates timestamped operational logs for install and debloat flows
- catalog load and cache save failures intentionally fall through to the next fallback layer
- invalid package identifiers are rejected by `InputValidation`

## Current Invariants

- `MainWindow.xaml.cs` should not own install/search/debloat business logic
- `Activate Windows` remains shell-only and out of `Initio.Core`
- tests reference `Initio.Core` directly; no unit test relies on a built app DLL
- UI automation runs against a built executable in test mode
