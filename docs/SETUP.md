# Development Setup

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Windows | 10 or 11 | WPF is Windows-only |
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| winget | Latest | Pre-installed on Windows 11; on Windows 10, install [App Installer](https://apps.microsoft.com/detail/9nblggh4nns1) from Microsoft Store |

No NuGet packages or external dependencies are required beyond the .NET 8 SDK.

## Installation

```powershell
git clone https://github.com/UriCaBa/Initio.git
cd Initio
dotnet restore
```

## Running Locally

```powershell
dotnet run
```

The application will launch with the embedded catalog (~200 apps) and attempt to fetch the latest remote catalog in the background. If winget is not installed, the app will still launch but installation features will be disabled.

## Building

```powershell
dotnet build
```

Output: `bin/Debug/net8.0-windows/Initio.exe`

## Configuration

### Catalog Source

The app loads its catalog from three sources in order:

1. **Remote**: `https://raw.githubusercontent.com/UriCaBa/Initio/main/catalog.json` (5s timeout)
2. **Cache**: `%APPDATA%/Initio/catalog_cache.json` (automatically saved on successful remote fetch)
3. **Embedded**: `catalog.json` compiled into the executable as an embedded resource

To modify the catalog, edit `catalog.json` at the project root. The structure:

```json
{
  "version": 1,
  "updatedAt": "2026-02-12",
  "categories": [
    {
      "name": "Category Name",
      "apps": [
        { "name": "Display Name", "wingetId": "Publisher.AppId" }
      ]
    }
  ]
}
```

### Themes

Theme files are in `Themes/`. To add a new theme:
1. Create `Themes/Theme.YourTheme.xaml` following the color key pattern in existing themes
2. Register it in the `ThemeOptions` list in `MainWindow.xaml.cs`

## Common Issues

| Issue | Solution |
|-------|----------|
| "winget not found" | Install App Installer from Microsoft Store, or run `winget --version` to verify |
| App launches but catalog is empty | Check network — the embedded fallback should always work; verify `catalog.json` is included as `EmbeddedResource` in `.csproj` |
| Build fails on non-Windows | WPF is Windows-only; this project cannot be built on macOS/Linux |
