# Build Executable Guide (Windows + PowerShell)

## 1. Open PowerShell in the project folder
> **PS (go to project folder):** move to the root where `NewPCSetupWPF.csproj` exists
```powershell
cd "C:\Users\Uri\Desktop\NewPCSetupWPF"
```

## 2. Check if .NET 8 SDK is installed
> **PS (check dotnet version):** verifies if the .NET CLI is available
```powershell
dotnet --version
```

If this command fails:
- Install `.NET 8 SDK (x64)` from the official Microsoft .NET download page.
- Close and reopen PowerShell.
- Run `dotnet --version` again.

## 3. Restore dependencies
> **PS (restore NuGet packages):** downloads required packages
```powershell
dotnet restore
```

## 4. Build in Release mode
> **PS (compile app):** builds optimized binaries
```powershell
dotnet build -c Release
```

## 5. Publish as a single executable
> **PS (publish self-contained EXE):** creates a standalone app for Windows x64
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 6. Open output folder
> **PS (open publish folder):** opens the folder containing the generated `.exe`
```powershell
explorer ".\bin\Release\net8.0-windows\win-x64\publish\"
```

## 7. Run your app
- Inside the `publish` folder, execute `NewPCSetupWPF.exe`.

## Optional: smaller executable (requires installed .NET runtime on target PC)
> **PS (publish framework-dependent):** smaller output, but target machine needs .NET runtime
```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```
