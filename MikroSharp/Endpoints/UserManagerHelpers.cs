using MikroSharp.Abstractions;
using MikroSharp.Core;
using System;
using System.Linq;

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
}