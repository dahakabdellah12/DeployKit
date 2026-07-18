# DeployKit 🚀

**Self-hosted update distribution server** for desktop applications. Each developer deploys their own instance — no central hosting, full control.

![GitHub](https://img.shields.io/github/license/dahakabdellah12/DeployKit)
![GitHub last commit](https://img.shields.io/github/last-commit/dahakabdellah12/DeployKit)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Docker](https://img.shields.io/badge/docker-ready-2496ED)

---

## Features

- **Self-hosted** — Deploy on your own server (Docker, Render, VPS)
- **Dashboard** — Web UI to register apps, generate API keys, upload updates
- **Update API** — Public endpoints for client apps to check / retrieve updates
- **Secure** — BCrypt passwords, Bearer token auth, rate limiting, CSP headers, audit logging, account lockout
- **Lightweight** — SQLite database, single Docker container (~200 MB)
- **Cross-platform** — Works on Windows, Linux, macOS
- **NuGet SDK** — One-line integration for WPF desktop apps

---

## Quick Start

### Docker (recommended)

```bash
git clone git@github.com:dahakabdellah12/DeployKit.git
cd DeployKit
docker compose up -d
```

Open `http://localhost:5000` and create your admin account.

> Data persists in a Docker volume (`deploykit-data`) at `/data`.

### Deploy to Render

1. Fork / push this repo to GitHub
2. Create a **Web Service** on Render → connect your repo
3. Choose **Docker** as the environment
4. Add a **Disk** mount at `/data` (1 GB)
5. Deploy — Render detects `render.yaml` automatically

### Manual (.NET CLI)

```bash
dotnet publish DeployKit.Cloud.Api -c Release -o out
ASPNETCORE_URLS=http://0.0.0.0:5000 dotnet out/DeployKit.Cloud.Api.dll
```

Set `DataPath` and `ConnectionStrings__Sqlite` for persistent storage.

---

## Usage

### First Run

1. Open the dashboard URL in your browser
2. Create the admin account (username + password)
3. Register your first app → get an **API Key**
4. Upload an update package

> **Single-user per instance.** Each developer hosts their own server.

### Client Integration (WPF)

Install the SDK:

```bash
dotnet add package DeployKit.Integration
```

Add one line to `App.xaml.cs`:

```csharp
DeployKit.Integration.DeployKit.Configure("YOUR_API_KEY", "https://your-server.com");
```

That's it. Updates are checked automatically and users are prompted to update.

### API — Check for Updates

```
GET /v1/check?X-Api-Key=YOUR_APP_KEY&v=1.0.0
```

```json
{
  "hasUpdate": true,
  "latestVersion": "1.1.0",
  "downloadUrl": "https://...",
  "packageHash": "sha256hex...",
  "isMandatory": false,
  "releaseNotes": "Bug fixes and improvements"
}
```

---

## API Reference

### Public Endpoints

| Method | Path | Auth | Rate Limit | Description |
|--------|------|------|------------|-------------|
| `GET` | `/v1/check` | `X-Api-Key` | 100/min | Check for updates |
| `GET` | `/v1/version` | `X-Api-Key` | 100/min | Get latest version |
| `GET` | `/v1/admin/status` | None | 100/min | Check if admin exists |

### Authenticated Endpoints (Bearer token)

| Method | Path | Rate Limit | Description |
|--------|------|------------|-------------|
| `POST` | `/v1/admin/setup` | 5/min | Create admin account |
| `POST` | `/v1/admin/login` | 5/min | Login |
| `POST` | `/v1/admin/refresh` | 30/min | Extend token expiry |
| `POST` | `/v1/admin/logout` | 30/min | Invalidate token |
| `POST` | `/v1/admin/register` | 30/min | Register a new app |
| `GET` | `/v1/admin/apps` | 30/min | List apps |
| `GET` | `/v1/admin/apps/{key}` | 30/min | App details + packages |
| `DELETE` | `/v1/admin/apps/{key}` | 30/min | Delete app |
| `POST` | `/v1/admin/apps/{key}/regenerate-key` | 30/min | Regenerate API key |
| `GET` | `/v1/admin/packages/{id}` | 30/min | Get package details |
| `PATCH` | `/v1/admin/packages/{id}` | 30/min | Update package |
| `DELETE` | `/v1/admin/packages/{id}` | 30/min | Delete package |
| `POST` | `/v1/upload` | 10/min | Upload new version |

---

## Security

| Control | Implementation |
|---------|---------------|
| **Passwords** | BCrypt (no minimum length / complexity) |
| **Auth tokens** | GUID, 8-hour expiry, sliding extension on each request |
| **Account lockout** | 5 failed attempts → 15-minute lock |
| **Rate limiting** | Sliding window per endpoint group |
| **CSP** | `default-src 'self'`, `script-src 'self'`, `style-src 'self' 'unsafe-inline'` |
| **HSTS** | Enabled |
| **Headers** | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin` |
| **Audit log** | All admin actions logged (IP, timestamp, user) |
| **Data encryption** | DPAPI with random 16-byte entropy (per-machine) |
| **Package integrity** | SHA256 hash verification on download and extraction |
| **Body limit** | 100 MB max request body |

---

## Project Structure

```
DeployKit/
├── DeployKit.Cloud.Api/     # Server — ASP.NET Core Minimal API
│   ├── Endpoints/           # API route handlers
│   ├── Helpers/             # Audit logging, auth
│   ├── Models/              # EF Core entities
│   ├── Data/                # DbContext + migrations
│   └── wwwroot/             # Dashboard SPA
├── DeployKit.Core/          # Core logic — manifest, patching, rollback
├── DeployKit.Gui/           # WPF GUI — build & upload update packages
└── DeployKit.Integration/   # NuGet SDK — one-line auto-update for WPF apps
```

---

## Tech Stack

- **Backend:** .NET 8, ASP.NET Core Minimal API, EF Core + SQLite
- **Frontend:** Vanilla JS (dark mode, Arabic RTL)
- **Auth:** BCrypt.Net-Next, Bearer tokens, DPAPI encryption
- **Client SDK:** .NET 8 WPF, NuGet package
- **Deployment:** Docker, Render, GitHub Actions CI/CD

---

## License

MIT — see [LICENSE](LICENSE).

---

## Contributors

Contributions are welcome! See [CONTRIBUTING](CONTRIBUTING.md).
