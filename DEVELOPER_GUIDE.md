# Developer Guide — دليل المطور الشامل

## Overview

DeployKit consists of **4 components** that work together:

```
┌─────────────────────────────────────┐
│  Developer's Machine                │
│  ┌──────────────┐  ┌──────────────┐│
│  │  DeployKit.Gui │  │ Desktop App ││
│  │  (Package      │  │ (WPF + SDK) ││
│  │   Builder)     │  │             ││
│  └──────┬───────┘  └──────┬───────┘│
└─────────┼──────────────────┼────────┘
          │ upload ZIP       │ check API
          ▼                  ▼
┌─────────────────────────────────────┐
│  Your Server (self-hosted)          │
│  ┌──────────────────────────────┐   │
│  │  DeployKit.Cloud.Api         │   │
│  │  (Dashboard + API)           │   │
│  └──────────────────────────────┘   │
└─────────────────────────────────────┘
```

---

## Part 1: Deploy the Server

### Option A — Docker (أسهل)

```bash
git clone git@github.com:dahakabdellah12/DeployKit.git
cd DeployKit
docker compose up -d
```

- Dashboard: `http://localhost:5000`
- Data persists in Docker volume `deploykit-data`
- To stop: `docker compose down`

### Option B — Render (سحابي)

1. Fork the repo to your GitHub
2. Go to [render.com](https://render.com) → New Web Service
3. Connect your forked repo
4. Choose **Docker** environment
5. Add Disk: mount `/data` (1 GB)
6. Deploy

Render detects `render.yaml` automatically.

### Option C — Manual (.NET 8)

```bash
dotnet publish DeployKit.Cloud.Api -c Release -o out

# Linux:
ASPNETCORE_URLS=http://0.0.0.0:5000 \
DataPath=/persistent/data \
ConnectionStrings__Sqlite="Data Source=/persistent/data/deploykit.db" \
dotnet out/DeployKit.Cloud.Api.dll

# Windows (PowerShell):
$env:ASPNETCORE_URLS='http://0.0.0.0:5000'
$env:DataPath='C:\deploykit-data'
dotnet out/DeployKit.Cloud.Api.dll
```

### First Run Setup

1. Open the dashboard URL in your browser
2. Click **"ليس لديك حساب؟ إنشاء حساب جديد"**
3. Enter username + password → click **إنشاء الحساب**

> **ملاحظة:** سيرفر واحد = مسؤول واحد فقط. لا يوجد multi-user.

---

## Part 2: Register an App

### Via Dashboard

1. After login, click **➕ تسجيل تطبيق**
2. Enter app name (e.g., `MyApp`)
3. Click **تسجيل**
4. Copy the generated **API Key** — you'll need it later

### Via API

```bash
curl -X POST http://localhost:5000/v1/admin/register \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"appName": "MyApp"}'
```

Response:
```json
{
  "appKey": "a1b2c3d4e5f6...",
  "appName": "MyApp"
}
```

---

## Part 3: Upload an Update Package

### What's in a package?

DeployKit uses **full packages** — ZIP files containing your app's files. The package includes a manifest with SHA256 hashes for every file.

### Using the GUI Builder (DeployKit.Gui)

1. Open **DeployKit.Gui.exe**
2. Go to **Build** tab
3. **New Directory:** Select the folder containing your published app files
4. **App Name:** Auto-detected from folder name
5. **Version:** Auto-detected from .exe version (or enter manually)
6. Click **بناء الحزمة (Build Package)**
7. After build:
   - **Output Path:** Where the `.zip` file was saved
   - **Download URL:** Where you'll host the ZIP (GitHub Releases, your server, etc.)

### Upload to Server

#### Via Dashboard
1. Click **📦 رفع تحديث**
2. Paste the **API Key**
3. Enter the **version** (e.g., `1.1.0`)
4. Enter the **Download URL** (where the ZIP is hosted)
5. Add release notes (optional)
6. Toggle **تحديث إجباري** if required
7. Click **رفع**

#### Via API

```bash
curl -X POST "http://localhost:5000/v1/upload?version=1.1.0&url=https://example.com/myapp-1.1.0.zip&notes=Bug+fixes&mandatory=true" \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN" \
  -H "X-Api-Key: YOUR_APP_KEY"
```

You can also include a SHA256 hash for integrity verification:

```bash
curl -X POST "http://localhost:5000/v1/upload?version=1.1.0&url=...&hash=abc123def456..."
```

---

## Part 4: Integrate the SDK into Your Desktop App

### Step 1: Install NuGet Package

```bash
dotnet add package DeployKit.Integration
```

### Step 2: One-Line Configuration

In your `App.xaml.cs` (WPF project), add **one line** in the constructor or `OnStartup`:

```csharp
using DeployKit.Integration;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DeployKit.Configure("YOUR_API_KEY", "https://your-server.com");
    }
}
```

That's it. The SDK will:

1. Detect your app's current version (from assembly)
2. Check the server for updates automatically
3. Show an update dialog if a new version is found

### Step 3: Customize (Optional)

```csharp
// Manually check for updates
var result = await DeployKit.CheckAsync();
if (result.HasUpdate)
{
    Console.WriteLine($"New version: {result.LatestVersion}");
    Console.WriteLine($"Mandatory: {result.IsMandatory}");
    Console.WriteLine($"Notes: {result.ReleaseNotes}");
}

// Get latest version string
string? latest = await DeployKit.GetLatestVersionAsync();

// Rollback to previous version
string error = await DeployKit.RollbackAsync();
if (string.IsNullOrEmpty(error))
    Console.WriteLine("Rollback successful!");

// Mark update as complete after manual update
DeployKit.MarkUpdateComplete("1.1.0");
```

### Check API Response

```json
{
  "hasUpdate": true,
  "latestVersion": "1.1.0",
  "downloadUrl": "https://github.com/.../myapp-1.1.0.zip",
  "packageHash": "a1b2c3d4e5f6...",
  "isMandatory": false,
  "releaseNotes": "Bug fixes and improvements"
}
```

### What the Update Window Looks Like

The SDK shows a built-in update dialog:

```
┌─────────────────────────────────┐
│          📦                      │
│      تحديث متوفر!               │
│    v1.0.0 ← v1.1.0              │
│                                 │
│  ┌─────────────────────────┐    │
│  │ Bug fixes and           │    │
│  │ improvements            │    │
│  └─────────────────────────┘    │
│                                 │
│  [⬇️ تحديث الآن]  [لاحقاً]      │
└─────────────────────────────────┘
```

---

## Part 5: Manual API Usage

### Check for updates

```
GET /v1/check
Headers: X-Api-Key: YOUR_APP_KEY
Query:   v=1.0.0
```

Example:
```bash
curl "https://your-server.com/v1/check?key=YOUR_APP_KEY&v=1.0.0"
```

### Get latest version only

```
GET /v1/version
Headers: X-Api-Key: YOUR_APP_KEY
```

### Manage apps (requires admin token)

```bash
# List all apps
curl -H "Authorization: Bearer TOKEN" https://your-server.com/v1/admin/apps

# Get app details
curl -H "Authorization: Bearer TOKEN" https://your-server.com/v1/admin/apps/APP_KEY

# Delete app
curl -X DELETE -H "Authorization: Bearer TOKEN" https://your-server.com/v1/admin/apps/APP_KEY

# Regenerate API key
curl -X POST -H "Authorization: Bearer TOKEN" https://your-server.com/v1/admin/apps/APP_KEY/regenerate-key
```

### Manage packages

```bash
# Update package info
curl -X PATCH -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"version":"1.2.0","releaseNotes":"New features"}' \
  https://your-server.com/v1/admin/packages/1

# Delete package
curl -X DELETE -H "Authorization: Bearer TOKEN" \
  https://your-server.com/v1/admin/packages/1
```

---

## Part 6: Complete Example Workflow

### Scenario: Shipping v1.1.0 of MyApp

```
1. BUILD THE APP
   dotnet publish MyApp -c Release -o publish/

2. BUILD THE PACKAGE
   Open DeployKit.Gui → Build tab
   New Directory: publish/
   App Name: MyApp (auto-detected)
   Version: 1.1.0 (auto-detected)
   Click "بناء الحزمة"
   → publish-myapp-1.1.0.zip created

3. HOST THE PACKAGE
   Upload the ZIP to GitHub Releases / your CDN
   Copy the download URL

4. REGISTER APP (first time only)
   Dashboard → ➕ تسجيل تطبيق → "MyApp"
   Copy the API Key

5. UPLOAD UPDATE
   Dashboard → 📦 رفع تحديث
   Paste API Key, enter version, paste download URL
   Click رفع

6. USERS GET UPDATES
   Users launch MyApp → SDK checks server → sees v1.1.0
   Update dialog shows → User clicks تحديث الآن
   Package downloads → SDK verifies SHA256 → extracts → done
```

---

## Part 7: Security Best Practices

| Practice | How |
|----------|-----|
| **Use HTTPS** | Render/Caddy/Nginx handle this automatically |
| **Protect your admin token** | Never share it; it expires after 8 hours of inactivity |
| **Use package hash** | Include SHA256 when uploading; SDK verifies on download |
| **Backup SQLite** | Copy `/data/deploykit.db` regularly |
| **Monitor audit log** | Check the `AuditLogs` table for unusual activity |
| **Update DeployKit** | Pull latest from GitHub and rebuild occasionally |

---

## Part 8: Troubleshooting

| Problem | Solution |
|---------|----------|
| Dashboard shows blank page | Check that the server is running and accessible |
| Login returns 429 | Wait 1 minute (rate limit: 5 attempts/min) |
| `"Account locked"` | Wait 15 minutes (5 failed attempts lockout) |
| SDK returns `"Cloud API returned 401"` | Check your API Key in `DeployKit.Configure()` |
| Docker: `connection refused` | Ensure ports are mapped: `docker compose ps` |
| Updates not detected | Check that the version in upload is **greater** than current |
| Package hash mismatch | The download URL points to a different file than expected |

---

## Architecture Notes

- **Auth tokens** are valid for 8 hours and auto-extend on each API call
- **Rate limits** are per-endpoint sliding window (Login: 5/min, Upload: 10/min, Admin: 30/min, API: 100/min)
- **Database**: SQLite at `/data/deploykit.db` (persistent Docker volume)
- **Encryption**: All stored tokens are DPAPI-encrypted with per-machine random entropy
- **Package verification**: SHA256 hash checked on both download and file extraction
- **Rollback file**: `rollback.json` in `%LOCALAPPDATA%\DeployKit\` is EFS-encrypted on Windows
