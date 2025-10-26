using MikroSharp.Models;
using System.Text.Json;

namespace MikroSharp.Abstractions;

public interface IUserManagerApi
{
    /// <summary>
    /// List all User-Manager users.
    /// </summary>
    Task<List<UmUser>> ListUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a specific User-Manager user by username.
    /// </summary>
    Task<UmUser> GetUserAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Try to search users by exact name using server-side filtering if supported by RouterOS.
    /// Returns zero or more matches; implementations should treat this as best-effort.
    /// </summary>
    Task<List<UmUser>> SearchUsersByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Get RouterOS internal user id (".id") for a username; returns null if not found.
    /// </summary>
    Task<string?> GetUserIdByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Call /user/monitor for a user by id with body {"once":true, ".id":"*n"}. Returns raw JSON.
    /// </summary>
    Task<JsonElement> MonitorUserByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Resolve user's id by name then call /user/monitor. Throws if user is not found.
    /// </summary>
    Task<JsonElement> MonitorUserAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// List user-to-profile links (each item links a user to a profile in User-Manager).
    /// </summary>
    Task<List<UmUserProfile>> ListUserProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// List user-to-profile entries for a specific user including runtime state and end-time.
    /// GET /rest/user-manager/user-profile?user={name}
    /// </summary>
    Task<List<UserProfileStatus>> ListUserProfilesByUserAsync(string user, CancellationToken ct = default);

    /// <summary>
    /// List available profiles.
    /// </summary>
    Task<List<ProfileEntry>> ListProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// List available limitations.
    /// </summary>
    Task<List<LimitationEntry>> ListLimitationsAsync(CancellationToken ct = default);

    /// <summary>
    /// List profile->limitation links.
    /// </summary>
    Task<List<ProfileLimitationEntry>> ListProfileLimitationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Create or update a user. Group will be set to "default".
    /// </summary>
    Task CreateOrUpdateUserAsync(string name, string password, int sharedUsers, CancellationToken ct = default);

    /// <summary>
    /// Patch arbitrary user fields. Property names will be serialized in kebab-case (dash-case).
    /// </summary>
    Task PatchUserAsync(string name, object body, CancellationToken ct = default);

    /// <summary>
    /// Patch arbitrary user fields. This method accepts a dictionary so callers can send exact API key names without relying on a naming policy.
    /// </summary>
    Task PatchUserAsync(string name, IDictionary<string, object?> body, CancellationToken ct = default);

    /// <summary>
    /// Set user's password.
    /// </summary>
    Task SetUserPasswordAsync(string name, string password, CancellationToken ct = default);

    /// <summary>
    /// Enable/disable user.
    /// </summary>
    Task DisableUserAsync(string name, bool disabled = true, CancellationToken ct = default);

    /// <summary>
    /// Delete a user.
    /// </summary>
    Task DeleteUserAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Set RADIUS-like attributes for the user (e.g. Mikrotik-Rate-Limit, Framed-IP-Address, Session-Timeout).
    /// Only provided values will be set; others are ignored.
    /// </summary>
    Task SetUserAttributesAsync(string name, string? rateLimit = null, string? staticIp = null, int? sessionTimeout = null, CancellationToken ct = default);

    /// <summary>
    /// Delete an existing user-profile link by its id.
    /// </summary>
    Task DeleteUserProfileAsync(string userProfileId, CancellationToken ct = default);

    /// <summary>
    /// Create a profile. If days &gt; 0, validity becomes "{days}d"; otherwise omitted.
    /// <paramref name="startMode"/> should be either "assigned" or "first-auth".
    /// </summary>
    Task CreateProfileAsync(string profileName, string startMode, int days, CancellationToken ct = default);

    /// <summary>
    /// Create a profile using a typed start mode.
    /// </summary>
    Task CreateProfileAsync(string profileName, StartWhenMode startMode, int days, CancellationToken ct = default);

    /// <summary>
    /// Create a limitation with transfer cap (GiB) and approximate time window (days).
    /// </summary>
    Task CreateLimitationAsync(string limitationName, int capGiB, int days, CancellationToken ct = default);

    /// <summary>
    /// Link a profile to a limitation.
    /// </summary>
    Task LinkProfileToLimitationAsync(string profileName, string limitationName, CancellationToken ct = default);

    /// <summary>
    /// Delete a profile->limitation link by its id.
    /// </summary>
    Task DeleteProfileLimitationAsync(string profileLimitationId, CancellationToken ct = default);

    /// <summary>
    /// Link a user to a profile.
    /// </summary>
    Task LinkUserToProfileAsync(string userName, string profileName, CancellationToken ct = default);
}