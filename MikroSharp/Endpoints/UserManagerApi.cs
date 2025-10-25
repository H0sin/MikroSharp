using MikroSharp.Abstractions;
using MikroSharp.Core;
using MikroSharp.Models;

namespace MikroSharp.Endpoints;

public class UserManagerApi(IApiConnection connection) : IUserManagerApi
{
    private const string BasePath = "/rest/user-manager";

    public Task<List<UmUser>> ListUsersAsync(CancellationToken ct = default) =>
        connection.GetAsync<List<UmUser>>($"{BasePath}/user", ct);

    public Task<UmUser> GetUserAsync(string name, CancellationToken ct = default) =>
        connection.GetAsync<UmUser>($"{BasePath}/user/{Uri.EscapeDataString(name)}", ct);

    public Task<List<UmUserProfile>> ListUserProfilesAsync(CancellationToken ct = default) =>
        connection.GetAsync<List<UmUserProfile>>($"{BasePath}/user-profile", ct);

    public Task<List<ProfileEntry>> ListProfilesAsync(CancellationToken ct = default) =>
        connection.GetAsync<List<ProfileEntry>>($"{BasePath}/profile", ct);

    public Task<List<LimitationEntry>> ListLimitationsAsync(CancellationToken ct = default) =>
        connection.GetAsync<List<LimitationEntry>>($"{BasePath}/limitation", ct);

    public Task<List<ProfileLimitationEntry>> ListProfileLimitationsAsync(CancellationToken ct = default) =>
        connection.GetAsync<List<ProfileLimitationEntry>>($"{BasePath}/profile-limitation", ct);

    public Task CreateOrUpdateUserAsync(string name, string password, int sharedUsers, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["password"] = password,
            ["group"] = "default",
            ["shared-users"] = sharedUsers
        };
        return connection.PutAsync($"{BasePath}/user", body, ct);
    }

    public Task PatchUserAsync(string name, object body, CancellationToken ct = default) =>
        connection.PatchAsync($"{BasePath}/user/{Uri.EscapeDataString(name)}", body, ct);

    public Task PatchUserAsync(string name, IDictionary<string, object?> body, CancellationToken ct = default) =>
        connection.PatchAsync($"{BasePath}/user/{Uri.EscapeDataString(name)}", body, ct);

    public Task SetUserPasswordAsync(string name, string password, CancellationToken ct = default) =>
        PatchUserAsync(name, new Dictionary<string, object?> { ["password"] = password }, ct);

    public Task DisableUserAsync(string name, bool disabled = true, CancellationToken ct = default) =>
        PatchUserAsync(name, new Dictionary<string, object?> { ["disabled"] = (disabled ? "yes" : "no") }, ct);

    public Task DeleteUserAsync(string name, CancellationToken ct = default) =>
        connection.DeleteAsync($"{BasePath}/user/{Uri.EscapeDataString(name)}", ct);

    public Task SetUserAttributesAsync(string name, string? rateLimit = null, string? staticIp = null,
        int? sessionTimeout = null, CancellationToken ct = default)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(rateLimit))
            values.Add("Mikrotik-Rate-Limit:" + rateLimit);
        if (!string.IsNullOrWhiteSpace(staticIp))
            values.Add("Framed-IP-Address:" + staticIp);
        if (sessionTimeout.HasValue)
            values.Add($"Session-Timeout:{sessionTimeout.Value}");

        string attributes = string.Join(",", values);
        return PatchUserAsync(name, new Dictionary<string, object?> { ["attributes"] = attributes }, ct);
    }

    public Task DeleteUserProfileAsync(string userProfileId, CancellationToken ct = default) =>
        // RouterOS .id values like "*13" must not be URL-encoded; leave '*' as-is
        connection.DeleteAsync($"{BasePath}/user-profile/{userProfileId}", ct);
    public Task CreateProfileAsync(string profileName, string startMode, int days, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = profileName,
            ["price"] = "0",
            ["starts-when"] = startMode,
            ["validity"] = days > 0 ? $"{days}d" : null
        };
        return connection.PutAsync($"{BasePath}/profile", body, ct);
    }

    public Task CreateProfileAsync(string profileName, StartWhenMode startMode, int days, CancellationToken ct = default)
        => CreateProfileAsync(profileName, startMode.ToApiValue(), days, ct);

    public Task CreateLimitationAsync(string limitationName, int capGiB, int days, CancellationToken ct = default)
    {
        int num = Math.Max(1, (int)Math.Round((days == 0 ? 30.0 : (double)days) / 30.0));
        long totalBytes = (long)capGiB * num * 1024L * 1024L * 1024L;

        var body = new Dictionary<string, object?>
        {
            ["name"] = limitationName,
            ["transfer-limit"] = $"{totalBytes}B"
        };
        return connection.PutAsync($"{BasePath}/limitation", body, ct);
    }

    public Task LinkProfileToLimitationAsync(string profileName, string limitationName, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["profile"] = profileName,
            ["limitation"] = limitationName
        };
        return connection.PutAsync($"{BasePath}/profile-limitation", body, ct);
    }

    public Task DeleteProfileLimitationAsync(string profileLimitationId, CancellationToken ct = default) =>
        // RouterOS .id values like "*13" must not be URL-encoded; leave '*' as-is
        connection.DeleteAsync($"{BasePath}/profile-limitation/{profileLimitationId}", ct);

    public Task LinkUserToProfileAsync(string userName, string profileName, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user"] = userName,
            ["profile"] = profileName
        };
        return connection.PutAsync($"{BasePath}/user-profile", body, ct);
    }
}