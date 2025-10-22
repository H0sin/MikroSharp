using MikroSharp.Abstractions;

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

        await um.CreateProfileAsync(profileName, startMode, days, ct);

        if (limitationName != null)
        {
            await um.CreateLimitationAsync(limitationName, capGiB, days, ct);
            await um.LinkProfileToLimitationAsync(profileName, limitationName, ct);
        }

        await um.LinkUserToProfileAsync(user, profileName, ct);
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
    /// Remove all current profile links for the user, then call <see cref="ApplyDynamicPlanAsync"/> to recreate.
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
        foreach (var oldProfile in oldProfiles.Where(p => p.User == user))
        {
            await um.DeleteUserProfileAsync(oldProfile.Id, ct);
        }

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