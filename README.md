# Initio 🚀

A modern WPF desktop application for quickly bootstrapping a fresh Windows PC by batch-installing apps via **winget**.

## Features
- **Quick Profiles**: One-click selection of apps for Dev, Home Office, Gaming, or custom use
- **Curated Catalog**: Default list of ~22 essential apps across categories  
- **Store Browser**: Browse ~100 trending apps by category, add to your catalog
- **Batch Install**: Install all selected apps sequentially with retry logic, ETA, and live logs
- **Modern UI**: Professional dark SaaS-style interface

## Requirements
- Windows 10/11
- .NET 8 SDK (for building)
- winget (App Installer) installed from Microsoft Store

## Build & Run
```powershell
cd ".\Initio"
dotnet run
```

## Publish Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Output: `bin\Release\net8.0-windows\win-x64\publish\Initio.exe`

## Distribution (GitHub Releases)
To share the app without code:
1.  Go to **Releases** > **Draft a new release** on GitHub.
2.  Tag it (e.g., `v1.0.0`).
3.  Upload the **`Initio.exe`** file from the `publish` folder above.
4.  Users can download just the `.exe` and run it (no install needed).

## Project Structure
```
NewPCSetupWPF/
├── App.xaml / .cs                  # Application entry point
├── MainWindow.xaml / .cs           # UI and Installation logic
├── Models/                         # App categories and catalogs
├── Themes/                         # Multi-theme dictionaries
├── Images/                         # Icons and assets
└── LICENSE                         # MIT License
```

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
