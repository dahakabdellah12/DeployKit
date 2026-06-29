# DeployKit 🚀

**Delta update engine + Cloud API + WPF GUI + NuGet SDK** — one-line auto-update for your .NET desktop apps.

---

## Overview

```
┌─────────────────────────────────────────────────────┐
│                   DeployKit                          │
├────────────┬────────────┬───────────┬───────────────┤
│ DeployKit  │ DeployKit  │ DeployKit │ DeployKit     │
│ .Core      │ .Cloud.Api │ .Gui      │ .Integration  │
│            │            │           │               │
│ Delta      │ REST API   │ WPF       │ NuGet SDK     │
│ Engine     │ + Dashboard│ Builder   │ (one-line)    │
│ (SHA256,   │ (Register, │ + Apply   │ + Updater     │
│  FNV-1a,   │  Upload,   │ UI        │  (standalone) │
│  AES-256)  │  Check,    │           │               │
│            │  Download, │           │               │
│            │  Admin)    │           │               │
└────────────┴────────────┴───────────┴───────────────┘
```

## Projects

| Project | Description | Target |
|---------|-------------|--------|
| `DeployKit.Core` | Delta engine: file comparison, SHA256 hashing, binary patching (FNV-1a), ZIP packaging, encryption (AES-256), rollback | `net8.0` |
| `DeployKit.Cloud.Api` | ASP.NET Minimal API: app registration, package upload, update check, download, admin dashboard | `net8.0` |
| `DeployKit.Gui` | WPF desktop UI to build and apply update packages | `net8.0-windows` |
| `DeployKit.Integration` | NuGet SDK: one-line `Configure()` → auto-check → update UI → launch updater | `net8.0-windows` |
| `DeployKit.Updater` | Standalone console: waits for app exit → applies delta → restarts app | `net8.0-windows` |
| `DeployKit.Tests` | xUnit tests for Core services (14 tests, all passing) | `net9.0` |

## Quick Start (SDK)

### 1. Install NuGet

```xml
<PackageReference Include="DeployKit.Integration" Version="1.0.0" />
```

### 2. Add one line

```csharp
// App.xaml.cs
DeployKit.Integration.DeployKit.Configure("YOUR_API_KEY", "https://your-cloud.com");
```

That's it. The SDK:
1. Reads your app version from the assembly
2. Checks the cloud for updates
3. If found — shows an **UpdateWindow** with release notes
4. On "Update Now" — downloads the package, launches `DeployKit.Updater.exe`, shuts down your app
5. The updater applies the delta update and restarts your app

### 3. Register your app & upload updates

```powershell
# Register (one time)
$reg = Invoke-RestMethod -Method Post "https://your-cloud.com/v1/register?name=MyApp"
$key = $reg.appKey   # ← save this in your app

# Upload new version
$bytes = [IO.File]::ReadAllBytes("update.zip")
Invoke-RestMethod -Method Post "https://your-cloud.com/v1/upload?key=$key&from=1.0.0&to=1.1.0" -Body $bytes -ContentType "application/octet-stream"
```

## Features

### Delta Updates
- **SHA256** per-file comparison — only changed files are packaged
- **FNV-1a binary patching** — for modified files, uses block-matching to create small binary patches
- Falls back to full file copy when patch is larger than 90% of the file

### Encryption
- **AES-256** optional encryption of update packages
- IV prepended to output; auto-decrypted on apply when key is provided
- `PackageBuilder(encryptionService)` → `BuildAsync()` encrypts automatically
- `PackageApplier(backupDir, encryptionService)` → decrypts on `ApplyAsync()`

### Rollback
- Backup created automatically before applying updates (via `RollbackService`)
- Rollback info saved to `%LOCALAPPDATA%/DeployKit/rollback.json`
- `DeployKit.RollbackAsync()` — restores previous version from backup
- `DeployKit.GetRollbackInfo()` — check if rollback is available
- Rollback button shown in UpdateWindow when backup exists
- Updater saves rollback info after successful update

### Web Dashboard
- Built-in admin UI at `/` (single-page RTL dashboard)
- Protected by `X-Admin-Key` header
- List/delete apps, view/delete packages, upload updates

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/register?name=X` | Register new app |
| POST | `/v1/upload?key=X&from=X&to=X` | Upload update package (raw body = ZIP) |
| GET | `/v1/check?key=X&v=X.X.X` | Check for updates |
| GET | `/v1/dl/{id}` | Download update package |
| GET | `/v1/admin/apps` | List all apps (admin key required) |
| GET | `/v1/admin/apps/{key}` | App detail with packages |
| DELETE | `/v1/admin/apps/{key}` | Delete app + packages |
| GET | `/v1/admin/packages/{id}` | Package detail |
| DELETE | `/v1/admin/packages/{id}` | Delete package |

## Running the Cloud API

### Local
```bash
cd DeployKit.Cloud.Api
dotnet run
# API at http://localhost:5000
# Dashboard at http://localhost:5000/
```

### Docker
```bash
docker build -t deploykit-api .
docker run -p 5000:80 -v deploykit-data:/data deploykit-api
```

### Render (free tier)
1. Fork/push this repo to GitHub
2. Create a new **Web Service** on Render
3. Connect your repo
4. Render auto-detects `render.yaml`
5. Set `AdminKey` environment variable
6. Deploy — note: free tier hibernates after inactivity

## Build & Test

```bash
# Build all
dotnet build DeployKit.sln

# Run tests
dotnet test DeployKit.Tests

# Pack NuGet
dotnet pack DeployKit.Integration -o artifacts

# Publish GUI
dotnet publish DeployKit.Gui -c Release -o dist/gui

# Publish Cloud API
dotnet publish DeployKit.Cloud.Api -c Release -o dist/api
```

## Update Package Format

Standard ZIP containing only changed files:

```
update.dkup
├── manifest.json       # Changes manifest (JSON)
├── new-file.dll        # Added/modified files
├── sub/folder.dll      # Files in subdirectories
├── patches/hash.patch  # Binary patches (smaller size)
```

## NuGet Package Contents

`DeployKit.Integration.1.0.0.nupkg`:
- `lib/net8.0-windows7.0/DeployKit.Integration.dll` — SDK
- `tools/DeployKit.Core.dll` — Delta engine (bundled)
- `tools/DeployKit.Updater.exe` + `.dll` — Standalone updater
- `build/DeployKit.Integration.targets` — Auto-copies tools to build output

## Requirements

- **SDK**: .NET 8, WPF app, `net8.0-windows`
- **Cloud API**: .NET 8, Docker or Render
- **Updater**: Windows (uses `Process.WaitForExit` + file I/O)
- **GUI Builder**: .NET 8, WPF

## Dependencies

- **Core**: None (pure .NET 8)
- **Cloud API**: EF Core + SQLite
- **Integration**: Core DLL (bundled), WPF (built-in)
- **Tests**: xUnit + Microsoft.NET.Test.Sdk

## License

MIT
