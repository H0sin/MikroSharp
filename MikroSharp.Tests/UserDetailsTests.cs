using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MikroSharp.Abstractions;
using MikroSharp.Endpoints;
using MikroSharp.Models;
using Xunit;

namespace MikroSharp.Tests;

public class UserDetailsTests
{
    private class FakeUmApi : IUserManagerApi
    {
        public UmUser? User { get; set; }
        public List<UmUserProfile> UserProfiles { get; set; } = new();
        public List<ProfileLimitationEntry> ProfileLimitations { get; set; } = new();
        public List<ProfileEntry> Profiles { get; set; } = new();
        public List<LimitationEntry> Limitations { get; set; } = new();

        public Task<List<UmUser>> ListUsersAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<UmUser> { User! });
        public Task<UmUser> GetUserAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(User!);
        public Task<List<UmUserProfile>> ListUserProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult(UserProfiles);
        public Task<List<UserProfileStatus>> ListUserProfilesByUserAsync(string user, CancellationToken ct = default) =>
            Task.FromResult(UserProfiles
                .Where(p => p.User == user)
                .Select(p => new UserProfileStatus(p.Id, p.User, p.Profile, State: null, EndTime: null))
                .ToList());
        public Task<List<ProfileEntry>> ListProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult(Profiles);
        public Task<List<LimitationEntry>> ListLimitationsAsync(CancellationToken ct = default) =>
            Task.FromResult(Limitations);
        public Task<List<ProfileLimitationEntry>> ListProfileLimitationsAsync(CancellationToken ct = default) =>
            Task.FromResult(ProfileLimitations);

        public Task<List<UmUser>> SearchUsersByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(new List<UmUser> { User! }.Where(u => u.Name == name).ToList());
        public Task<string?> GetUserIdByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult<string?>(User != null && User.Name == name ? "*1" : null);
        public Task<System.Text.Json.JsonElement> MonitorUserByIdAsync(string id, CancellationToken ct = default)
        {
            using var doc = System.Text.Json.JsonDocument.Parse("{\"active-sessions\":\"0\",\"active-sub-sessions\":\"0\",\"actual-profile\":\"BASIC\",\"total-download\":\"100\",\"total-upload\":\"200\",\"total-uptime\":\"1h\"}");
            return Task.FromResult(doc.RootElement.Clone());
        }
        public async Task<System.Text.Json.JsonElement> MonitorUserAsync(string name, CancellationToken ct = default)
        {
            var id = await GetUserIdByNameAsync(name, ct) ?? "*0";
            return await MonitorUserByIdAsync(id, ct);
        }

        public Task CreateOrUpdateUserAsync(string name, string password, int sharedUsers, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task PatchUserAsync(string name, object body, CancellationToken ct = default) => Task.CompletedTask;
        public Task PatchUserAsync(string name, IDictionary<string, object?> body, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetUserPasswordAsync(string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisableUserAsync(string name, bool disabled = true, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteUserAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetUserAttributesAsync(string name, string? rateLimit = null, string? staticIp = null, int? sessionTimeout = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteUserProfileAsync(string userProfileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProfileAsync(string profileName, string startMode, int days, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProfileAsync(string profileName, StartWhenMode startMode, int days, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateLimitationAsync(string limitationName, int capGiB, int days, CancellationToken ct = default) => Task.CompletedTask;
        public Task LinkProfileToLimitationAsync(string profileName, string limitationName, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteProfileLimitationAsync(string profileLimitationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task LinkUserToProfileAsync(string userName, string profileName, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetUserDetailsAsync_Should_Aggregate_Profiles_Limitations_And_Parse_Attributes()
    {
        var api = new FakeUmApi
        {
            User = new UmUser(
                Name: "alice",
                Group: "default",
                Disabled: "no",
                SharedUsers: "2",
                Attributes: "Mikrotik-Rate-Limit:512k/1M,Framed-IP-Address:10.0.0.5,Session-Timeout:3600"),
            UserProfiles = new List<UmUserProfile>
            {
                new("*1", "alice", "BASIC"),
                new("*2", "alice", "BASIC"), // duplicate should be deduped
                new("*3", "ALICE", "PREMIUM") // case-insensitive user match
            },
            ProfileLimitations = new List<ProfileLimitationEntry>
            {
                new("*10","BASIC","1GB-30"),
                new("*11","BASIC","1GB-30"), // duplicate lim should be deduped
                new("*12","PREMIUM","UL-INF")
            }
        };

        var details = await UserManagerHelpers.GetUserDetailsAsync(api, "alice");

        details.User.Name.Should().Be("alice");
        details.RateLimit.Should().Be("512k/1M");
        details.StaticIp.Should().Be("10.0.0.5");
        details.SessionTimeout.Should().Be(3600);

        details.Profiles.Should().HaveCount(2);
        var basic = details.Profiles.Single(p => p.Profile == "BASIC");
        basic.Limitations.Should().BeEquivalentTo(new[] { "1GB-30" });
        var premium = details.Profiles.Single(p => p.Profile == "PREMIUM");
        premium.Limitations.Should().BeEquivalentTo(new[] { "UL-INF" });
    }
}
