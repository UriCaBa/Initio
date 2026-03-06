# Deployment

## Overview

Initio is intended to ship as a self-contained `win-x64` single-file executable.

Important project settings in `NewPCSetupWPF.csproj`:
- `PublishSingleFile=true`
- `SelfContained=true`
- `RuntimeIdentifier=win-x64`
- `PublishReadyToRun=true`
- `IncludeNativeLibrariesForSelfExtract=true`

## Publish Command

```powershell
dotnet publish NewPCSetupWPF.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Publish Output

```text
bin\Release\net8.0-windows\win-x64\publish\Initio.exe
```

## Debug Executable

If you only need a local build for manual testing, use:

```text
bin\Debug\net8.0-windows\win-x64\Initio.exe
```

## Distribution Notes

- The executable contains the embedded catalog, so the app can still boot offline.
- Remote catalog refresh is accepted only when the downloaded catalog matches a trusted embedded SHA-256 fingerprint.
- Real install flows still depend on `winget` being present on the target machine.
- Test mode is a development and automation feature; it is enabled only through the `INITIO_TEST_MODE` environment variable.

## Release Integrity

Before publishing a GitHub release:

1. Sign `Initio.exe` with Authenticode if you have a code-signing certificate.
2. Generate and publish a SHA-256 checksum for the exact release binary.
3. Keep the embedded catalog in sync with the trusted remote catalog content used for that release.
4. Include the checksum and verification steps in the release notes.

Example checksum command:

```powershell
Get-FileHash .\bin\Release\net8.0-windows\win-x64\publish\Initio.exe -Algorithm SHA256
```

## Release Checklist

1. Build the app in Debug and verify the shell boots.
2. Run `dotnet test Tests\Initio.Tests.csproj`.
3. Run `dotnet test Tests.UI\Initio.UITests.csproj` in an interactive desktop session.
4. Publish the Release executable.
5. Smoke-test the published `Initio.exe` on a Windows machine with `winget` installed.
6. Generate and record the SHA-256 checksum for the published binary.
7. Sign the executable if code signing is available.
8. Attach the checksum/signing details to the GitHub release.
