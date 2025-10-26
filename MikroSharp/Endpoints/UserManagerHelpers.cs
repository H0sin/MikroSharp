using MikroSharp.Abstractions;
using MikroSharp.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MikroSharp.Models;
using System.Text.Json;

namespace MikroSharp.Endpoints;

public static class UserManagerHelpers
{
    private static (string profileName, string? limitationName) BuildNames(int days, int capGiB, int sharedUsers,
        string startMode)
    {
        string d = (days <= 0) ? "INF" : $"{days}D";
        string u = (sharedUsers <= 0) ? "INFU" : $"{sharedUsers}U";
        string s = (startMode == "first-auth") ? "onhold" : "active";

        if (capGiB == 0)
            return ($"UL-{d}-{u}-{s}", null);

        return ($"{capGiB}GB-{d}-{u}-{s}", $"{capGiB}GB-{d}");
    }

    /// <summary>
    /// Enable a user (shorthand for DisableUserAsync(name, false)).
    /// </summary>
    public static Task EnableUserAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
        => um.DisableUserAsync(name, false, ct);

    /// <summary>
    /// Set Mikrotik-Rate-Limit for a user.
    /// </summary>
    public static Task SetRateLimitAsync(this IUserManagerApi um, string name, string rateLimit, CancellationToken ct = default)
        => um.SetUserAttributesAsync(name, rateLimit: rateLimit, ct: ct);

    /// <summary>
    /// Set a static framed IP address for a user.
    /// </summary>
    public static Task SetStaticIpAsync(this IUserManagerApi um, string name, string staticIp, CancellationToken ct = default)
        => um.SetUserAttributesAsync(name, staticIp: staticIp, ct: ct);

    /// <summary>
    /// Set Session-Timeout (seconds) for a user.
    /// </summary>
    public static Task SetSessionTimeoutAsync(this IUserManagerApi um, string name, int sessionTimeoutSeconds, CancellationToken ct = default)
        => um.SetUserAttributesAsync(name, sessionTimeout: sessionTimeoutSeconds, ct: ct);

    /// <summary>
    /// Create or update a user, set optional attributes, create (or reuse) a profile and optional limitation,
    /// and link the user to the profile. Names are generated dynamically based on inputs.
    /// </summary>
    public static async Task ApplyDynamicPlanAsync(
        this IUserManagerApi um,
        string user,
        string password,
        int days,
        int capGiB,
        int sharedUsers,
        string startMode = "assigned",
        string? rateLimit = null,
        string? staticIp = null,
        CancellationToken ct = default)
    {
        await um.CreateOrUpdateUserAsync(user, password, sharedUsers, ct);
        await um.SetUserAttributesAsync(user, rateLimit, staticIp, ct: ct);
        
        (string profileName, string? limitationName) = BuildNames(days, capGiB, sharedUsers, startMode);

        // Reuse existing profile if present; tolerate duplicate on create
        var existingProfiles = await um.ListProfilesAsync(ct);
        bool profileExists = existingProfiles.Any(p => string.Equals((p.Name ?? string.Empty).Trim(), (profileName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        if (!profileExists)
        {
            try
            {
                await um.CreateProfileAsync(profileName, startMode, days, ct);
            }
            catch (MikroSharpException ex)
            {
                var code = (int?)ex.StatusCode;
                if (code is 400 or 409)
                {
                    var body = ex.ResponseBody ?? string.Empty;
                    if (!(body.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                          body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                          body.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        if (limitationName != null)
        {
            // Reuse existing limitation if present; tolerate duplicate on create
            var existingLimitations = await um.ListLimitationsAsync(ct);
            bool limitationExists = existingLimitations.Any(l => string.Equals((l.Name ?? string.Empty).Trim(), (limitationName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            if (!limitationExists)
            {
                try
                {
                    await um.CreateLimitationAsync(limitationName, capGiB, days, ct);
                }
                catch (MikroSharpException ex)
                {
                    var code = (int?)ex.StatusCode;
                    if (code == 400 || code == 409)
                    {
                        var body = ex.ResponseBody ?? string.Empty;
                        if (!(body.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                              body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                              body.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Check existing profile->limitation links and only create one if none exist
            var existingProfileLimitations = await um.ListProfileLimitationsAsync(ct);
            var matches = existingProfileLimitations
                .Where(pl => string.Equals((pl.Profile ?? string.Empty).Trim(), (profileName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                             && string.Equals((pl.Limitation ?? string.Empty).Trim(), (limitationName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            // If at least one link exists, just use it as-is without deleting duplicates.
            // Only create a link if none exist.
            if (matches.Count == 0)
            {
                // No existing link; create one. If the API reports a duplicate, swallow as fallback.
                try
                {
                    await um.LinkProfileToLimitationAsync(profileName, limitationName, ct);
                }
                catch (MikroSharpException ex)
                {
                    var code = (int?)ex.StatusCode;
                    if (code == 400 || code == 409)
                    {
                        var body = ex.ResponseBody ?? string.Empty;
                        if (body.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                            body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                            body.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                        {
                            // ignore duplicate link
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        // Link user to profile; skip if already linked; tolerate duplicate error
        var userProfiles = await um.ListUserProfilesAsync(ct);
        bool alreadyLinked = userProfiles.Any(p => string.Equals((p.User ?? string.Empty).Trim(), (user ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals((p.Profile ?? string.Empty).Trim(), (profileName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        if (!alreadyLinked)
        {
            try
            {
                await um.LinkUserToProfileAsync(user, profileName, ct);
            }
            catch (MikroSharpException ex)
            {
                var code = (int?)ex.StatusCode;
                if (code == 400 || code == 409)
                {
                    var body = ex.ResponseBody ?? string.Empty;
                    if (!(body.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                          body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                          body.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Strongly-typed overload of <see cref="ApplyDynamicPlanAsync(MikroSharp.Abstractions.IUserManagerApi,string,string,int,int,int,string,string,string,System.Threading.CancellationToken)"/>.
    /// </summary>
    public static Task ApplyDynamicPlanAsync(
        this IUserManagerApi um,
        string user,
        string password,
        int days,
        int capGiB,
        int sharedUsers,
        StartWhenMode startMode,
        string? rateLimit = null,
        string? staticIp = null,
        CancellationToken ct = default)
        => um.ApplyDynamicPlanAsync(user, password, days, capGiB, sharedUsers, startMode.ToApiValue(), rateLimit, staticIp, ct);

    /// <summary>
    /// Remove all current profile links for the user, then call <see cref="ApplyDynamicPlanAsync(MikroSharp.Abstractions.IUserManagerApi,string,string,int,int,int,string,string,string,System.Threading.CancellationToken)"/> to recreate.
    /// </summary>
    public static async Task RenewDynamicPlanAsync(
        this IUserManagerApi um,
        string user,
        string password,
        int days,
        int capGiB,
        int sharedUsers,
        string startMode = "assigned",
        string? rateLimit = null,
        string? staticIp = null,
        CancellationToken ct = default)
    {
        var oldProfiles = await um.ListUserProfilesAsync(ct);
        foreach (var oldProfile in oldProfiles.Where(p => string.Equals(p.User, user, StringComparison.OrdinalIgnoreCase)))
        {
            await um.DeleteUserProfileAsync(oldProfile.Id, ct);
        }

        await um.DeleteUserAsync(user,ct);
        await um.ApplyDynamicPlanAsync(user, password, days, capGiB, sharedUsers, startMode, rateLimit, staticIp, ct);
    }

    /// <summary>
    /// Strongly-typed overload of <see cref="RenewDynamicPlanAsync(MikroSharp.Abstractions.IUserManagerApi,string,string,int,int,int,string,string,string,System.Threading.CancellationToken)"/>.
    /// </summary>
    public static Task RenewDynamicPlanAsync(
        this IUserManagerApi um,
        string user,
        string password,
        int days,
        int capGiB,
        int sharedUsers,
        StartWhenMode startMode,
        string? rateLimit = null,
        string? staticIp = null,
        CancellationToken ct = default)
        => um.RenewDynamicPlanAsync(user, password, days, capGiB, sharedUsers, startMode.ToApiValue(), rateLimit, staticIp, ct);

    private static (string? RateLimit, string? StaticIp, int? SessionTimeout) ParseAttributes(string? attributes)
    {
        string? rateLimit = null;
        string? staticIp = null;
        int? sessionTimeout = null;

        if (!string.IsNullOrWhiteSpace(attributes))
        {
            var parts = attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split(':', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                var value = kv[1].Trim();
                if (key.Equals("Mikrotik-Rate-Limit", StringComparison.OrdinalIgnoreCase))
                    rateLimit = value;
                else if (key.Equals("Framed-IP-Address", StringComparison.OrdinalIgnoreCase))
                    staticIp = value;
                else if (key.Equals("Session-Timeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var seconds)) sessionTimeout = seconds;
                }
            }
        }

        return (rateLimit, staticIp, sessionTimeout);
    }

    /// <summary>
    /// Aggregate full information about a user: base user entry, assigned profiles, and limitations per profile,
    /// plus parsed attributes like rate-limit, static IP and session-timeout.
    /// </summary>
    public static async Task<UserDetails> GetUserDetailsAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
    {
        var user = await um.GetUserAsync(name, ct);

        // Profiles linked to this user
        var allUserProfiles = await um.ListUserProfilesAsync(ct);
        var userProfiles = allUserProfiles
            .Where(p => string.Equals((p.User ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Profile)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Map profiles to their limitations
        var profileLimitations = await um.ListProfileLimitationsAsync(ct);
        var perProfile = new List<UserProfileDetails>();
        foreach (var profile in userProfiles)
        {
            var lims = profileLimitations
                .Where(pl => string.Equals((pl.Profile ?? string.Empty).Trim(), (profile ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(pl => pl.Limitation)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            perProfile.Add(new UserProfileDetails(profile, lims));
        }

        var (rateLimit, staticIp, sessionTimeout) = ParseAttributes(user.Attributes);

        return new UserDetails(user, perProfile, rateLimit, staticIp, sessionTimeout, null);
    }

    /// <summary>
    /// Minimal details: combines GetUser with /user/monitor result. Useful for quick checks (no profiles/limitations).
    /// </summary>
    public static async Task<UserWithMonitor> GetUserWithMonitorAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
    {
        var user = await um.GetUserAsync(name, ct);
        var monitor = await um.MonitorUserAsync(name, ct);
        return new UserWithMonitor(user, monitor);
    }

    /// <summary>
    /// Minimal user details using monitor to confirm existence: returns UmUser and parsed attributes only.
    /// Does NOT fetch profiles or limitations. Uses /user/monitor via POST with {"once":true,".id":"*n"}.
    /// </summary>
    public static async Task<UserDetails> GetUserDetailsMinimalAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
    {
        // Resolve RouterOS .id first
        var id = await um.GetUserIdByNameAsync(name, ct);
        
        if (string.IsNullOrWhiteSpace(id))
            throw new MikroSharpException($"User '{name}' not found.", "POST", "/rest/user-manager/user/monitor", System.Net.HttpStatusCode.NotFound, null);

        // Call monitor and capture payload (consumption/active info)
        JsonElement monitorJson;
        
        try
        {
            monitorJson = await um.MonitorUserByIdAsync(id!, ct);
        }
        catch (MikroSharpException ex)
        {
            throw new MikroSharpException($"Failed to monitor user '{name}': {ex.Message}", ex.Method, ex.Path, ex.StatusCode, ex.ResponseBody, ex);
        }

        // Map monitor payload to strongly-typed model
        UserMonitorInfo? monitor = null;
        try
        {
            monitor = monitorJson.Deserialize<UserMonitorInfo>();
        }
        catch
        {
            // If deserialization fails, keep it null rather than throwing
        }

        // Fetch user model; if GET by name fails (404/500), fallback to search/list
        UmUser? user = null;
        try
        {
            user = await um.GetUserAsync(name, ct);
        }
        catch (MikroSharpException)
        {
            var matches = await um.SearchUsersByNameAsync(name, ct);
            user = matches.FirstOrDefault(u => string.Equals((u.Name ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                var all = await um.ListUsersAsync(ct);
                user = all.FirstOrDefault(u => string.Equals((u.Name ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        if (user is null)
            throw new MikroSharpException($"User '{name}' not found after monitor.", "GET", $"/rest/user-manager/user/{name}", System.Net.HttpStatusCode.NotFound, null);

        var (rateLimit, staticIp, sessionTimeout) = ParseAttributes(user.Attributes);
        return new UserDetails(user, new List<UserProfileDetails>(), rateLimit, staticIp, sessionTimeout, monitor);
    }

    /// <summary>
    /// Fetches current account status for the specified user by calling:
    /// - GET /rest/user-manager/user-profile?user={name} for profile link state and expiry (end-time)
    /// - POST /rest/user-manager/user/monitor with {"once":true, ".id":"*n"} for counters and actual profile
    /// Returns a combined model. If the user id cannot be resolved, monitor will be null.
    /// </summary>
    public static async Task<UserAccountStatus> GetUserAccountStatusAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
    {
        // Profile statuses including end-time/state
        var profiles = await um.ListUserProfilesByUserAsync(name, ct);

        // Monitor info is optional if we cannot resolve id
        UserMonitorInfo? monitor = null;
        var id = await um.GetUserIdByNameAsync(name, ct);
        if (!string.IsNullOrWhiteSpace(id))
        {
            JsonElement monitorJson;
            try
            {
                monitorJson = await um.MonitorUserByIdAsync(id!, ct);
            }
            catch (MikroSharpException)
            {
                monitorJson = default;
            }

            if (monitorJson.ValueKind != JsonValueKind.Undefined && monitorJson.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    // RouterOS may return an array with a single object, or a bare object
                    if (monitorJson.ValueKind == JsonValueKind.Array)
                    {
                        var arr = monitorJson.EnumerateArray();
                        if (arr.MoveNext())
                            monitor = arr.Current.Deserialize<UserMonitorInfo>();
                    }
                    else if (monitorJson.ValueKind == JsonValueKind.Object)
                    {
                        monitor = monitorJson.Deserialize<UserMonitorInfo>();
                    }
                }
                catch
                {
                    // ignore parse failures
                }
            }
        }

        return new UserAccountStatus(name, profiles, monitor);
    }
}