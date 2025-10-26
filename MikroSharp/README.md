# MikroSharp

A modern, strongly-typed .NET client for MikroTik RouterOS REST API (User-Manager focused), with clean ergonomics, typed helpers, robust error handling, and explicit dash-case request keys.

- .NET 9 target (works with .NET 8+ via multi-targeting in the future)
- Simple client setup with Basic Auth
- Explicit dash-case (kebab-case) request keys matching RouterOS REST field names
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

### Get full user details (user + profiles + limitations)

You can fetch an aggregated view of a user, including the base user entry, linked profiles, the limitations attached to each profile, and parsed attributes (rate-limit, static IP, session-timeout):

```csharp
using MikroSharp.Endpoints; // for GetUserDetailsAsync extension

var details = await client.UserManager.GetUserDetailsAsync("alice");

// Base user object from RouterOS
Console.WriteLine($"User: {details.User.Name}, Disabled: {details.User.Disabled}, Shared: {details.User.SharedUsers}");

// Parsed attributes (if present)
Console.WriteLine($"RateLimit: {details.RateLimit}, StaticIp: {details.StaticIp}, SessionTimeout: {details.SessionTimeout}");

// Profiles and their linked limitations
foreach (var p in details.Profiles)
{
    Console.WriteLine($"Profile: {p.Profile} -> Limitations: {string.Join(", ", p.Limitations)}");
}
```

Notes:
- Duplicated links are de-duplicated in the result.
- Matching is case-insensitive for usernames and profile names.
- Unknown or missing attributes are handled gracefully (nulls).

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

## JSON field names (dash-case)

RouterOS REST uses dash-case (kebab-case) field names such as `shared-users`, `starts-when`, and `transfer-limit`.
To ensure exact compatibility and avoid mistakes, MikroSharp sends request bodies using explicit dictionaries with the exact field names expected by the API. This avoids relying on automatic naming policies.

Examples of keys we send:
- `name`, `password`, `group`, `shared-users`
- `starts-when`, `validity`, `price`
- `transfer-limit`
- `user`, `profile`, `limitation`

Responses are still deserialized into models using `[JsonPropertyName]` where needed (e.g., `.id`, `shared-users`).

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

## Tests

This repository includes a small test suite to prevent regressions around request body field names. In particular, we verify outgoing JSON uses the exact dash-case keys expected by RouterOS (e.g., `group`, not `g-roup`).

Run tests:

```bash
dotnet test MikroSharp.Tests/MikroSharp.Tests.csproj -v minimal
```

## Roadmap

- Add more RouterOS endpoints (e.g., IP address management, DHCP, Hotspot, PPP)
- Add unit tests and integration test harness (with RouterOS container or simulator)
- Multi-targeting (net8.0, net9.0)

## Contributing

Issues and PRs are welcome. Please include a clear reproduction, RouterOS version, and the REST endpoint you’re targeting.

## License

MIT
