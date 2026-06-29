# DeployKit Integration SDK

One-line auto-update SDK for .NET WPF desktop apps.

## Quick Start

```csharp
// App.xaml.cs — add this in OnStartup
DeployKit.Integration.DeployKit.Configure("YOUR_API_KEY", "https://deploykit-api.onrender.com");
```

The SDK automatically:
1. Reads your app version from `Assembly.GetEntryAssembly().GetName().Version`
2. Checks the cloud: `GET /v1/check?key=...&v=1.0.0`
3. If update exists → shows **UpdateWindow** (WPF) with "Update Now" / "Later"
4. On "Update Now": downloads the package, launches `DeployKit.Updater.exe`, shuts down your app
5. Updater waits for your app to exit → applies delta update → restarts your app

## Manual Check (Optional)

```csharp
var result = await DeployKit.Integration.DeployKit.CheckAsync();
if (result.HasUpdate)
    Console.WriteLine($"Update available: v{result.LatestVersion}");
```

## Set App Version

In your `.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
</PropertyGroup>
```

SDK reads this and sends it to the cloud on each check.

## Requirements

- .NET 8.0-windows
- WPF application (for the update notification window)
- DeployKit Cloud API (see [DeployKit](https://github.com/dahakabdellah12/DeployKit))

## How It Works

```
Your App → DeployKit.Configure(key, url)
              │
              ├─ Auto-detect version (1.0.0)
              ├─ GET /v1/check?key=X&v=1.0.0
              │
              ├─ hasUpdate=false → silent skip
              │
              └─ hasUpdate=true  → show UpdateWindow
                   ├─ "Update Now"  → download ZIP → launch updater.exe → exit
                   └─ "Later"       → dismiss
```

## API Endpoints (Cloud)

| Method | Path | Description |
|---|---|---|
| POST | `/v1/register?name=X` | Register app → returns `appKey` |
| POST | `/v1/upload?key=X&from=X&to=X` | Upload update package (raw ZIP body) |
| GET | `/v1/check?key=X&v=X.X.X` | Check for available update |
| GET | `/v1/dl/{id}` | Download update package |

## NuGet Package Contents

When you install this package, your project gets:
- `DeployKit.Integration.dll` — the SDK
- `DeployKit.Core.dll` — delta update engine (bundled)
- `DeployKit.Updater.exe` — standalone updater (copied to output on build)
