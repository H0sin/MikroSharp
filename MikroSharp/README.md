# MikroSharp

A modern, strongly-typed .NET client for MikroTik RouterOS REST API (User-Manager focused), with clean ergonomics, typed helpers, and robust error handling.

- .NET 9 target (works with .NET 8+ via multi-targeting in the future)
- Simple client setup with Basic Auth
- Kebab-case JSON naming policy out of the box to match RouterOS REST field names
- Clear models and XML docs for IntelliSense
- High-level helper extensions for common tasks (creating profiles/limitations, linking, setting attributes)

Note: RouterOS REST must be enabled on the device. Some operations may require specific packages or RouterOS versions.

## Installation

NuGet (when available):

```bash
# Coming soon (package metadata is prepared)
dotnet add package MikroSharp
```

From source (ProjectReference):

```xml
<ItemGroup>
  <ProjectReference Include="../MikroSharp/MikroSharp.csproj" />
</ItemGroup>
```

## Quick start

```csharp
using MikroSharp;
using MikroSharp.Abstractions;
using MikroSharp.Endpoints;

// For production with a valid TLS certificate
var client = new MikroSharpClient("https://192.168.88.1", "admin", "password");

// Or for lab/testing environments ONLY (skips TLS validation)
// var client = MikroSharpClient.CreateInsecure("https://192.168.88.1", "admin", "password");

// List users
var users = await client.UserManager.ListUsersAsync();

// Create or update a user
await client.UserManager.CreateOrUpdateUserAsync("alice", "p@ssw0rd", sharedUsers: 1);

// Set handy attributes
await client.UserManager.SetRateLimitAsync("alice", "20M/20M");
await client.UserManager.SetStaticIpAsync("alice", "10.10.10.50");
await client.UserManager.SetSessionTimeoutAsync("alice", 3600);

// Disable/Enable
await client.UserManager.DisableUserAsync("alice", disabled: true);
await client.UserManager.EnableUserAsync("alice");

// Clean up
await client.UserManager.DeleteUserAsync("alice");
```

### Configure with options

You can also configure the client using an options object (timeouts, extra headers, disposal control), and optionally tweak the JSON serializer.

```csharp
using MikroSharp.Abstractions;
using System.Text.Json;

var options = new MikroSharpOptions
{
    BaseUrl = "https://192.168.88.1",
    Username = "admin",
    Password = "password",
    Timeout = TimeSpan.FromSeconds(30),
    DefaultHeaders = new Dictionary<string, string>
    {
        ["X-Trace-Id"] = Guid.NewGuid().ToString()
    },
    DisposeHttpClient = true
};

var client = new MikroSharpClient(options, configureJson: json =>
{
    // customize serializer if you need to (e.g., add converters)
});
```

## Profiles and limitations

The API exposes low-level methods and high-level helpers. You can compose your own flows.

Low-level:

```csharp
// Create a profile (start mode can be "assigned" or "first-auth")
await client.UserManager.CreateProfileAsync("30D-UL-1U", startMode: "assigned", days: 30);

// Create a limitation (cap in GiB, days used to estimate a transfer window)
await client.UserManager.CreateLimitationAsync("100GB-30D", capGiB: 100, days: 30);

// Link the profile to the limitation
await client.UserManager.LinkProfileToLimitationAsync("30D-UL-1U", "100GB-30D");

// Link the user to the profile
await client.UserManager.LinkUserToProfileAsync("alice", "30D-UL-1U");
```

Typed overloads and helpers:

```csharp
using MikroSharp.Abstractions; // StartWhenMode

// Typed start mode overload
await client.UserManager.CreateProfileAsync("ProPlan", StartWhenMode.Assigned, days: 30);

// One-shot dynamic plan helper
await client.UserManager.ApplyDynamicPlanAsync(
    user: "alice",
    password: "p@ssw0rd",
    days: 30,
    capGiB: 50,       // 0 means Unlimited plan
    sharedUsers: 1,
    startMode: StartWhenMode.Assigned,
    rateLimit: "20M/20M",
    staticIp: null
);

// Renew dynamic plan (removes previous user-profile links first)
await client.UserManager.RenewDynamicPlanAsync(
    user: "alice",
    password: "p@ssw0rd",
    days: 30,
    capGiB: 50,
    sharedUsers: 1,
    startMode: StartWhenMode.Assigned,
    rateLimit: "20M/20M"
);
```

## Model helpers

`UmUser` frequently returns some fields as strings. Use the helpers to make them easy to consume:

```csharp
using MikroSharp.Models;

var user = await client.UserManager.GetUserAsync("alice");

bool disabled = user.IsDisabled();           // parses "yes"/"no"
int? shared = user.SharedUsersAsInt();       // parses "1" => 1
```

## Public API overview

Namespaces of interest:
- MikroSharp: main client `MikroSharpClient`
- MikroSharp.Abstractions: interfaces and enums (`IMikroSharpClient`, `IUserManagerApi`, `StartWhenMode`, `MikroSharpOptions`)
- MikroSharp.Endpoints: endpoint implementations and helper extensions (`UserManagerApi`, `UserManagerHelpers`)
- MikroSharp.Models: models returned from the REST API

Key interfaces:

- `IMikroSharpClient`
  - `IUserManagerApi UserManager` – access to User-Manager operations

- `IUserManagerApi`
  - List/Get: `ListUsersAsync`, `GetUserAsync`, `ListUserProfilesAsync`, `ListProfilesAsync`, `ListLimitationsAsync`
  - Users: `CreateOrUpdateUserAsync`, `PatchUserAsync`, `SetUserPasswordAsync`, `DisableUserAsync`, `DeleteUserAsync`
  - Attributes: `SetUserAttributesAsync` (builds Mikrotik-Rate-Limit, Framed-IP-Address, Session-Timeout)
  - Profiles & limitations: `CreateProfileAsync`, `CreateLimitationAsync`, `LinkProfileToLimitationAsync`, `LinkUserToProfileAsync`

Helpers (extension methods on `IUserManagerApi`):
- `EnableUserAsync`, `SetRateLimitAsync`, `SetStaticIpAsync`, `SetSessionTimeoutAsync`
- `ApplyDynamicPlanAsync`, `RenewDynamicPlanAsync`

## JSON naming policy (dash-case)

RouterOS REST commonly uses dash-case (kebab-case) field names (e.g., `shared-users`, `starts-when`).
MikroSharp configures a custom `System.Text.Json` naming policy that converts .NET property names to dash-case.

Implications:
- When sending bodies (e.g., `PatchUserAsync(name, new { Disabled = "yes" })`), your PascalCase property names are converted to the correct dash-case names automatically.
- Models use `[JsonPropertyName]` where needed, to align with RouterOS response fields like `.id`, `shared-users`.

## Error handling

All HTTP failures and network errors are wrapped in `MikroSharp.Core.MikroSharpException`, which includes:
- `StatusCode` (if available)
- `ResponseBody` (raw text)
- `Method` and `Path` for quick diagnostics

```csharp
try
{
    await client.UserManager.DeleteUserAsync("missing-user");
}
catch (MikroSharp.Core.MikroSharpException ex)
{
    Console.WriteLine($"Failed: {ex.Message}\nStatus: {ex.StatusCode}\nPath: {ex.Path}\nBody: {ex.ResponseBody}");
}
```

## SSL and security

- Prefer valid TLS certificates on your router. For labs, use `MikroSharpClient.CreateInsecure(...)` to bypass cert validation.
- Credentials are sent using HTTP Basic Auth; always use HTTPS.

## Lifetime and threading

`MikroSharpClient` implements `IDisposable` and owns an internal `HttpClient`. It's safe to reuse across many calls and threads. Dispose the client when done.

```csharp
await using var client = new MikroSharpClient("https://router", "admin", "password");
// use client...
```

## Roadmap

- Add more RouterOS endpoints (e.g., IP address management, DHCP, Hotspot, PPP)
- Add unit tests and integration test harness (with RouterOS container or simulator)
- Multi-targeting (net8.0, net9.0)

## Contributing

Issues and PRs are welcome. Please include a clear reproduction, RouterOS version, and the REST endpoint you’re targeting.

## License

MIT
