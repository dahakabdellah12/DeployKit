# DeployKit.Integration

One-line auto-update SDK for .NET WPF desktop apps.

```csharp
DeployKit.Configure("your-api-key", "https://your-cloud-api.com");
```

Checks for updates, shows a download progress UI, and applies updates automatically.

Full package updates only — client compares file hashes locally and downloads a complete ZIP from a URL you provide.
