# Deployment

## Overview

Initio is distributed as a single self-contained `.exe` file. No installer, no runtime dependencies on the target machine. Users download and run.

## Building for Release

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Output**: `bin/Release/net8.0-windows/win-x64/publish/Initio.exe`

### Publish configuration (from `.csproj`)

| Setting | Value | Effect |
|---------|-------|--------|
| `PublishSingleFile` | true | All assemblies bundled into one `.exe` |
| `SelfContained` | true | .NET runtime included — no SDK needed on target |
| `RuntimeIdentifier` | win-x64 | 64-bit Windows target |
| `IncludeNativeLibrariesForSelfExtract` | true | Native libs embedded in single file |

## Distribution via GitHub Releases

1. Build the release executable (see above)
2. Go to the repository on GitHub > **Releases** > **Draft a new release**
3. Create a tag (e.g., `v1.0.0`)
4. Upload `Initio.exe` from the publish folder
5. Add release notes describing changes
6. Publish the release

Users download the `.exe` and run it directly — no installation step required.

## Target Environment

| Requirement | Details |
|-------------|---------|
| OS | Windows 10 or 11 (64-bit) |
| Runtime | None (self-contained) |
| winget | Required for app installation features; app launches without it but disables install functionality |
| Disk space | ~70 MB for the executable |
| Network | Optional — embedded catalog works offline; network needed for remote catalog updates and winget operations |
