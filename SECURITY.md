# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x     | ✅ Active development |

## Reporting a Vulnerability

If you discover a security vulnerability, please **do not open a public issue**. Instead, send a private report to the repository maintainer via GitHub's security advisory feature:

1. Go to https://github.com/dahakabdellah12/DeployKit/security/advisories
2. Click **New draft security advisory**
3. Fill in the details

You can expect an acknowledgment within 48 hours and a fix timeline in the response.

## Security Practices

- **No secrets in source code** — all tokens are DPAPI-encrypted or runtime-generated
- **NuGet Audit** — enabled on all projects with `NuGetAuditMode=all`
- **Lock files** — `packages.lock.json` committed for deterministic restore
- **Dependabot** — enabled for automatic dependency updates
- **Static analysis** — CI builds with `-warnaserror`

## Disclosure Policy

Once a fix is released, we will publish a security advisory on GitHub and credit the reporter (if desired).
