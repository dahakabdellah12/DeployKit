# Changelog

All notable changes to this project are documented here.

## [1.1.0] — 2026-07-18

### Security
- **Critical**: GitHub PAT removed from remote URL, switched to SSH
- CORS: `new Uri(origin)` → `Uri.TryCreate` to prevent 500 on malformed origins
- Added `UseHttpsRedirection()` middleware
- Added rate limiting to `/v1/admin/refresh` endpoint
- Added max-length validation for `AppName` (128), `ReleaseNotes` (2000), `notes` (2000), `hash` (64)
- Added hex validation for package hash in upload endpoint
- Fixed XSS: `esc()` now escapes single quotes
- Fixed XSS: `p.id` and `packageCount` now properly escaped in DOM
- Added `.dockerignore` to prevent build context bloat
- Added `appsettings.Production.json` to `.gitignore`
- CI: switched from .NET 9 SDK to .NET 8 SDK
- Added `.gitattributes` for line-ending normalization
- Removed multi-user feature — single admin per instance
- Removed password complexity requirements
- CSP: `script-src 'self'` with no `unsafe-inline`

### Changes since v1.0.0
- Full-package update support with SHA256 integrity verification
- DPAPI encryption for all stored tokens (random 16-byte entropy)
- Account lockout after 5 failed login attempts (15 min)
- Sliding window rate limiting (replaced fixed window)
- Session token: 8-hour expiry with sliding extension on each API call
- Security headers: HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy
- Audit logging for all admin actions (API keys masked to last 4 chars)
- EF Core migrations instead of `EnsureCreated()`
- Arabic RTL dashboard with dark mode
- NuGet SDK: single-line auto-update for WPF apps
- Docker + Render deployment support
