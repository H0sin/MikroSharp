using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroSharp.Abstractions;
using MikroSharp.Core;
using MikroSharp.Models;
using System.Collections.Generic;

namespace MikroSharp.Endpoints;

public static class UserManagerSafeHelpers
{
    /// <summary>
    /// Best-effort renewal: tries to delete existing user-profile links for the user, but ignores
    /// common RouterOS delete failures (404/409/500 with indicative messages), then reapplies the plan.
    /// </summary>
    public static async Task RenewDynamicPlanBestEffortAsync(
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
            try
            {
                await um.DeleteUserProfileAsync(oldProfile.Id, ct);
            }
            catch (MikroSharpException ex)
            {
                var code = (int?)ex.StatusCode;
                if (code == 404 || code == 409 || code == 500)
                {
                    var body = ex.ResponseBody ?? string.Empty;
                    if (body.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("no such", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("exist", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase))
                    {
                        // Best-effort delete: ignore and continue
                        continue;
                    }
                }
                // Otherwise, rethrow
                throw;
            }
        }

        await um.ApplyDynamicPlanAsync(user, password, days, capGiB, sharedUsers, startMode, rateLimit, staticIp, ct);
    }

    /// <summary>
    /// Strongly-typed overload of <see cref="RenewDynamicPlanBestEffortAsync(MikroSharp.Abstractions.IUserManagerApi,string,string,int,int,int,string,string,string,System.Threading.CancellationToken)"/>.
    /// </summary>
    public static Task RenewDynamicPlanBestEffortAsync(
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
        => um.RenewDynamicPlanBestEffortAsync(user, password, days, capGiB, sharedUsers, startMode.ToApiValue(), rateLimit, staticIp, ct);

    /// <summary>
    /// Best-effort fetch: tries to return full user details. If GET /user/{name} fails with 404/500 (some RouterOS builds return 500 for missing users),
    /// it falls back to ListUsers and assembles details from there. Returns null if the user truly does not exist.
    /// </summary>
    public static async Task<UserDetails?> GetUserDetailsBestEffortAsync(this IUserManagerApi um, string name, CancellationToken ct = default)
    {
        try
        {
            return await UserManagerHelpers.GetUserDetailsAsync(um, name, ct);
        }
        catch (MikroSharpException ex)
        {
            var code = (int?)ex.StatusCode;
            var body = ex.ResponseBody ?? string.Empty;
            bool isLikelyMissingOrRouterBug = code is 404 or 500 &&
                                              (string.IsNullOrWhiteSpace(body) ||
                                               body.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                               body.Contains("no such", StringComparison.OrdinalIgnoreCase) ||
                                               body.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase));
            if (!isLikelyMissingOrRouterBug)
                throw;

            // Fallback 1: server-side search by name if available
            var matches = await um.SearchUsersByNameAsync(name, ct);
            var user = matches.FirstOrDefault(u => string.Equals((u.Name ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

            // Fallback 2: full list if search returned nothing
            if (user is null)
            {
                var allUsers = await um.ListUsersAsync(ct);
                user = allUsers.FirstOrDefault(u => string.Equals((u.Name ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (user is null)
                return null;

            // Collect profiles and limitations
            var allUserProfiles = await um.ListUserProfilesAsync(ct);
            var userProfiles = allUserProfiles
                .Where(p => string.Equals((p.User ?? string.Empty).Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Profile)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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

            // Parse attributes locally
            (string? rate, string? ip, int? to) = ParseAttributes(user.Attributes);
            return new UserDetails(user, perProfile, rate, ip, to, null);
        }
    }

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
}
