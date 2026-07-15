# DeployKit

Self-hosted update distribution server for desktop applications. Manage app updates, generate API keys, and distribute differential update packages — all from your own infrastructure.

## Features

- **Self-hosted** — Deploy on your own server (Docker, Render, VPS)
- **Multi-user** — Each developer manages their own apps with full isolation
- **Dashboard** — Web UI to register apps, generate API keys, upload updates
- **Update API** — Public endpoints for client apps to check/retrieve updates
- **Secure** — BCrypt passwords, Bearer token auth, rate limiting, CSP headers, audit logging
- **Lightweight** — SQLite database, single Docker container
- **Cross-platform** — Works on Windows, Linux, macOS

## Quick Start

### Docker (recommended)

```bash
git clone https://github.com/dahakabdellah12/DeployKit.git
cd DeployKit
docker compose up -d
```

Open `http://localhost:5000` and create your first admin account.

### Deploy to Render

1. Fork/push this repo to GitHub
2. Create a **Web Service** on Render → connect your repo
3. Choose **Docker** as the environment
4. Add a **Disk** mount at `/data` (1 GB) for persistent SQLite storage
5. Deploy — Render detects `render.yaml` automatically

### Manual (any .NET host)

```bash
dotnet publish DeployKit.Cloud.Api -c Release -o out
ASPNETCORE_URLS=http://0.0.0.0:5000 dotnet out/DeployKit.Cloud.Api.dll
```

Set env var `DataPath=/persistent/data` and `ConnectionStrings__Sqlite=Data Source=/persistent/data/deploykit.db` for persistent storage.

## Usage

### First Run

1. Open the dashboard URL in your browser
2. Create the first admin account (username + password)
3. Start managing your apps

### For Developers

Each developer on the same server:

1. Logs in with their own credentials
2. Registers apps → gets an **API Key**
3. Uploads update packages via the dashboard
4. Embeds the API Key in their desktop app to check for updates

### Client App Integration

Your desktop app checks for updates by calling the public API:

```
GET /v1/check?X-Api-Key=YOUR_APP_KEY&v=1.0.0
```

Response:
```json
{
  "hasUpdate": true,
  "latestVersion": "1.1.0",
  "downloadUrl": "https://...",
  "isMandatory": false,
  "releaseNotes": "Bug fixes and improvements"
}
```

Use the **DeployKit.Integration** NuGet package or call the API directly from your app.

## API Reference

### Public Endpoints (no auth)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/check` | Check for updates (requires `X-Api-Key` header + `?v=x.y.z`) |
| `GET` | `/v1/version` | Get latest version (requires `X-Api-Key` header) |
| `GET` | `/v1/admin/status` | Check if any user exists |

### Authenticated Endpoints (Bearer token)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/admin/setup` | Create first user |
| `POST` | `/v1/admin/register` | Register a new app |
| `POST` | `/v1/admin/users` | Add a new user |
| `POST` | `/v1/admin/login` | Login |
| `POST` | `/v1/admin/logout` | Logout (invalidates token) |
| `GET` | `/v1/admin/apps` | List user's apps |
| `GET` | `/v1/admin/apps/{key}` | App details with packages |
| `DELETE` | `/v1/admin/apps/{key}` | Delete app |
| `POST` | `/v1/admin/apps/{key}/regenerate-key` | Regenerate API key |
| `PATCH` | `/v1/admin/packages/{id}` | Update package |
| `DELETE` | `/v1/admin/packages/{id}` | Delete package |
| `POST` | `/v1/upload` | Upload new version (requires Bearer + `X-Api-Key`) |

## Security

- **Passwords**: BCrypt hashed with complexity requirements (8+ chars, uppercase, digit)
- **Tokens**: Bearer tokens compared using timing-safe `FixedTimeEquals`
- **Rate Limiting**: Login (5/min), Upload (10/min), Admin (30/min), API (100/min)
- **Headers**: CSP, HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy
- **Audit Log**: All admin actions logged with timestamp, IP, and user
- **App Isolation**: Each user sees only their own registered apps

## Tech Stack

- **Backend**: .NET 8, ASP.NET Core Minimal API
- **Database**: SQLite (via EF Core)
- **Frontend**: Vanilla JS (no framework), dark mode UI
- **Auth**: BCrypt.Net-Next, Bearer tokens
- **Deployment**: Docker, Render

## License

MIT — see [LICENSE](LICENSE).
