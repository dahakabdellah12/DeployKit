# DeployKit Integration SDK

One-line update SDK for .NET desktop apps.

## Usage

```csharp
// 1. Configure with your API key from DeployKit Cloud
DeployKit.Integration.DeployKit.Configure("your_api_key_here");

// Done! The SDK will automatically:
// - Detect your app's current version
// - Check for updates on startup
// - Show a notification when an update is available
// - Download and apply the update
```

## Manual check

```csharp
var result = await DeployKit.Integration.DeployKit.CheckAsync();
if (result.HasUpdate)
{
    // Update available!
    Console.WriteLine($"New version: {result.LatestVersion}");
}
```

## Requirements
- .NET 8.0-windows
- WPF application
