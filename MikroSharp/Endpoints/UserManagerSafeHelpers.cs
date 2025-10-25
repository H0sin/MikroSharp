using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroSharp.Abstractions;
using MikroSharp.Core;

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
}
