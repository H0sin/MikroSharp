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

    public Task CreateOrUpdateUserAsync(string name, string password, int sharedUsers, CancellationToken ct = default)
    {
        var body = new
        {
            Name = name,
            Password = password,
            Group = "default",
            SharedUsers = sharedUsers
        };
        return connection.PutAsync($"{BasePath}/user", body, ct);
    }

    public Task PatchUserAsync(string name, object body, CancellationToken ct = default) =>
        connection.PatchAsync($"{BasePath}/user/{Uri.EscapeDataString(name)}", body, ct);

    public Task SetUserPasswordAsync(string name, string password, CancellationToken ct = default) =>
        PatchUserAsync(name, new { Password = password }, ct);

    public Task DisableUserAsync(string name, bool disabled = true, CancellationToken ct = default) =>
        PatchUserAsync(name, new { Disabled = (disabled ? "yes" : "no") }, ct);

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
        return PatchUserAsync(name, new { Attributes = attributes }, ct);
    }

    public Task DeleteUserProfileAsync(string userProfileId, CancellationToken ct = default) =>
        connection.DeleteAsync($"{BasePath}/user-profile/{Uri.EscapeDataString(userProfileId)}", ct);

    public Task CreateProfileAsync(string profileName, string startMode, int days, CancellationToken ct = default)
    {
        return connection.PutAsync($"{BasePath}/profile", new
        {
            Name = profileName,
            Price = "0",
            StartsWhen = startMode, // kebab-case via policy: "starts-when"
            Validity = days > 0 ? $"{days}d" : null
        }, ct);
    }

    public Task CreateProfileAsync(string profileName, StartWhenMode startMode, int days, CancellationToken ct = default)
        => CreateProfileAsync(profileName, startMode.ToApiValue(), days, ct);

    public Task CreateLimitationAsync(string limitationName, int capGiB, int days, CancellationToken ct = default)
    {
        int num = Math.Max(1, (int)Math.Round((days == 0 ? 30.0 : (double)days) / 30.0));
        long totalBytes = (long)capGiB * num * 1024L * 1024L * 1024L;

        return connection.PutAsync($"{BasePath}/limitation", new
        {
            Name = limitationName,
            TransferLimit = $"{totalBytes}B" // kebab-case via policy: "transfer-limit"
        }, ct);
    }

    public Task LinkProfileToLimitationAsync(string profileName, string limitationName, CancellationToken ct = default)
    {
        return connection.PutAsync($"{BasePath}/profile-limitation", new
        {
            Profile = profileName,
            Limitation = limitationName
        }, ct);
    }

    public Task LinkUserToProfileAsync(string userName, string profileName, CancellationToken ct = default)
    {
        return connection.PutAsync($"{BasePath}/user-profile", new
        {
            User = userName,
            Profile = profileName
        }, ct);
    }
}