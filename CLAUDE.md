# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
dotnet run                    # Run the app
dotnet build                  # Build only
dotnet test                   # Run all tests (unit + UI)
dotnet test --filter "FullyQualifiedName~CatalogServiceTests"   # Unit tests only
dotnet test --filter "FullyQualifiedName~InitioAppTests"        # UI tests only
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true  # Publish .exe
```

## Architecture

**Initio** is a single-window WPF desktop app (.NET 8) that batch-installs Windows apps via winget. No external NuGet packages — only standard .NET libraries.

### Partial class split (critical to understand)

`MainWindow` is split across three files that form a single class:
- **MainWindow.xaml.cs** (~900 lines): UI initialization, theme switching, profile selection, catalog management, ICollectionView filtering/sorting, property notifications
- **MainWindow.Install.cs**: All winget process execution — installation loop, retry logic, cancellation, progress/ETA tracking, `winget list` refresh
- **MainWindow.Debloat.cs**: Bloatware detection and removal — PowerShell-based AppX package scanning and batch removal with retry/progress/cancellation

Both files are in the `NewPCSetupWPF` namespace. When adding functionality, put it in the correct partial file based on whether it's UI/catalog logic or winget/installation logic.

### Data binding pattern

MainWindow implements `INotifyPropertyChanged` directly (no separate ViewModel). Models (`AppItem`, `StoreTrendItem`, `BloatwareItem`) also implement it. Collections use `ObservableCollection<T>` wrapped in `ICollectionView` for filtering and sorting.

Key: `AppItem.IsInstalled = true` automatically sets `IsSelected = false` — this is intentional behavior, not a bug.

### Catalog system

Four `ObservableCollection`s drive the four tabs:
- `_appItems` (AppItem) — "My Setup" tab: user's selected apps
- `_storeTrendItems` (StoreTrendItem) — "Store by Category" tab: remote catalog
- `_wingetSearchResults` (StoreTrendItem) — "Full Search" tab: live winget search
- `_bloatwareItems` (BloatwareItem) — "Debloater" tab: pre-installed bloatware detection/removal

`CatalogService` loads the store catalog with a 3-tier fallback: remote GitHub JSON (5s timeout) -> `%APPDATA%/Initio/catalog_cache.json` -> embedded resource. The embedded resource is `catalog.json` at project root, compiled via `<EmbeddedResource>` in the .csproj.

### Winget integration

All winget interaction goes through `RunWingetCommandAsync()` in `MainWindow.Install.cs`, which runs `winget` as a `System.Diagnostics.Process` on a background thread. `WingetSearchService` also spawns winget processes but is a separate static service for search-only operations.

### Theme system

5 XAML ResourceDictionaries in `Themes/`. All UI elements use `DynamicResource` bindings. Themes are swapped at runtime by replacing the merged dictionary. When adding new UI elements, always use `DynamicResource` (not `StaticResource`) for theme colors.

## Conventions

- Namespace: `NewPCSetupWPF` (root), `NewPCSetupWPF.Models`, `NewPCSetupWPF.Services`, `NewPCSetupWPF.Converters`
- Models are `sealed class` with `INotifyPropertyChanged` and private `SetField<T>` helper
- Immutable properties use `required init`; bindable properties use `get/set` with `SetField`
- Services are `static class` with async methods
- UI thread dispatch via `Dispatcher.InvokeAsync()` when updating from background threads
- Logging via `AppendLog(string)` which is thread-safe and auto-scrolls

## Testing

- Tests live in `Tests/` at the project root (unit tests with xUnit)
- Always create tests for new features — use `/dotnet-testing` skill for guidance
- Always run `dotnet build && dotnet test` after changes (build first due to DLL reference pattern)
- Test naming: `Method_Scenario_ExpectedResult`
- Group tests with `// ═══ Section Name ═══` separators
- Never claim done with failing tests

## Verification Workflow

After implementing a new feature, always:
1. `dotnet build` — 0 errors
2. `dotnet build && dotnet test` — all tests pass
3. `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` — publish the .exe
4. Launch the .exe so the user can visually verify the result
5. Output path: `bin/Release/net8.0-windows/win-x64/publish/Initio.exe`

## Gotchas

- Tests are excluded from the main WPF compilation via `<Compile Remove="Tests\**" />` in .csproj — they are standalone files, not a separate test project
- The `catalog.json` at project root is both the embedded resource AND the remote source (same file pushed to GitHub)
- `MainWindow.xaml` is ~80KB — large XAML file with inline styles, control templates, and all three tab layouts
- Windows-only: WPF cannot build or run on macOS/Linux
