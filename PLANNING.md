# Initio — NewPCSetupWPF Project Planning

## Overview
Desktop WPF application (.NET 8) for bootstrapping a fresh Windows PC by batch-installing apps via **winget**. The user selects from a curated catalog (or adds custom apps), picks a quick-start profile, and runs the installation in one click.

## Architecture
- **Framework**: .NET 8 WPF (WindowStyle=None, custom chrome)
- **Pattern**: Code-behind with INotifyPropertyChanged (single MainWindow)
- **Models**: `AppItem`, `StoreTrendItem`, `StoreTrendCatalog` in `Models/`
- **Themes**: Resource dictionaries in `Themes/` (color brushes + base control styles)
- **Build**: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

## Key Features
1. **Default Catalog** — User's pre-selected list of ~22 apps across categories
2. **Quick Profiles** — Default, Essentials, Dev Rig, Home Office, Gaming, Custom
3. **Store by Category** — ~100 trending apps in 5 categories (Productivity, Comms, Media, Dev, Gaming)
4. **Installation Engine** — Sequential winget install with retry, ETA, progress, live logs
5. **Theme System** — Single unified dark theme (professional SaaS look)

## Design Principles
- Modern dark SaaS aesthetic with sidebar navigation, card-based app tiles
- Single polished theme instead of multiple nearly-identical ones
- Clean MVVM-like separation while keeping practical code-behind approach
- Files capped at 500 lines; split into modules as needed
- All non-obvious logic documented with comments

## Build & Publish
```powershell
cd "C:\Users\Uri\Desktop\NewPCSetupWPF"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Output: `bin\Release\net8.0-windows\win-x64\publish\NewPCSetupWPF.exe`
