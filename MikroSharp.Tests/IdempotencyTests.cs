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

public class IdempotencyTests
{
    private class FakeUserManagerApi : IUserManagerApi
    {
        public List<UmUser> Users { get; } = new();
        public List<ProfileEntry> Profiles { get; } = new();
        public List<UmUserProfile> UserProfiles { get; } = new();
        public List<LimitationEntry> Limitations { get; } = new();
        public List<ProfileLimitationEntry> ProfileLimitations { get; } = new();
        public int CreateProfileCalls { get; private set; }
        public int LinkUserToProfileCalls { get; private set; }

        public Task<List<UmUser>> ListUsersAsync(CancellationToken ct = default) => Task.FromResult(Users);
        public Task<UmUser> GetUserAsync(string name, CancellationToken ct = default)
            => Task.FromResult(Users.First(u => u.Name == name));
        public Task<List<UmUserProfile>> ListUserProfilesAsync(CancellationToken ct = default)
            => Task.FromResult(UserProfiles.ToList());
        public Task<List<ProfileEntry>> ListProfilesAsync(CancellationToken ct = default)
            => Task.FromResult(Profiles.ToList());
        public Task<List<LimitationEntry>> ListLimitationsAsync(CancellationToken ct = default)
            => Task.FromResult(Limitations.ToList());
        public Task<List<ProfileLimitationEntry>> ListProfileLimitationsAsync(CancellationToken ct = default)
            => Task.FromResult(ProfileLimitations.ToList());

        public Task<List<UmUser>> SearchUsersByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(Users.Where(u => u.Name == name).ToList());
        public Task<string?> GetUserIdByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult<string?>(Users.Any(u => u.Name == name) ? "*1" : null);
        public Task<System.Text.Json.JsonElement> MonitorUserByIdAsync(string id, CancellationToken ct = default)
        {
            using var doc = System.Text.Json.JsonDocument.Parse("{\"active\":false}");
            return Task.FromResult(doc.RootElement.Clone());
        }
        public async Task<System.Text.Json.JsonElement> MonitorUserAsync(string name, CancellationToken ct = default)
        {
            var id = await GetUserIdByNameAsync(name, ct) ?? "*0";
            return await MonitorUserByIdAsync(id, ct);
        }

        public Task CreateOrUpdateUserAsync(string name, string password, int sharedUsers, CancellationToken ct = default)
        {
            var existing = Users.FirstOrDefault(u => u.Name == name);
            if (existing is null)
            {
                Users.Add(new UmUser(
                    Name: name,
                    Group: "default",
                    Disabled: "no",
                    SharedUsers: sharedUsers.ToString(),
                    Attributes: null
                ));
            }
            return Task.CompletedTask;
        }
        public Task PatchUserAsync(string name, object body, CancellationToken ct = default) => Task.CompletedTask;
        public Task PatchUserAsync(string name, IDictionary<string, object?> body, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetUserPasswordAsync(string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisableUserAsync(string name, bool disabled = true, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteUserAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetUserAttributesAsync(string name, string? rateLimit = null, string? staticIp = null, int? sessionTimeout = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteUserProfileAsync(string userProfileId, CancellationToken ct = default)
        {
            UserProfiles.RemoveAll(up => up.Id == userProfileId);
            return Task.CompletedTask;
        }
        public Task CreateProfileAsync(string profileName, string startMode, int days, CancellationToken ct = default)
        {
            CreateProfileCalls++;
            if (!Profiles.Any(p => p.Name == profileName))
                Profiles.Add(new ProfileEntry(Id: profileName, Name: profileName));
            return Task.CompletedTask;
        }
        public Task CreateProfileAsync(string profileName, StartWhenMode startMode, int days, CancellationToken ct = default)
            => CreateProfileAsync(profileName, startMode.ToApiValue(), days, ct);
        public Task CreateLimitationAsync(string limitationName, int capGiB, int days, CancellationToken ct = default)
        {
            if (!Limitations.Any(l => l.Name == limitationName))
                Limitations.Add(new LimitationEntry(Id: limitationName, Name: limitationName));
            return Task.CompletedTask;
        }
        public Task LinkProfileToLimitationAsync(string profileName, string limitationName, CancellationToken ct = default)
        {
            if (!ProfileLimitations.Any(pl => pl.Profile == profileName && pl.Limitation == limitationName))
                ProfileLimitations.Add(new ProfileLimitationEntry(Id: profileName + "/" + limitationName, Profile: profileName, Limitation: limitationName));
            return Task.CompletedTask;
        }
        public Task DeleteProfileLimitationAsync(string profileLimitationId, CancellationToken ct = default)
        {
            ProfileLimitations.RemoveAll(pl => pl.Id == profileLimitationId);
            return Task.CompletedTask;
        }
        public Task LinkUserToProfileAsync(string userName, string profileName, CancellationToken ct = default)
        {
            LinkUserToProfileCalls++;
            if (!UserProfiles.Any(p => p.User == userName && p.Profile == profileName))
            {
                UserProfiles.Add(new UmUserProfile(Id: userName + "/" + profileName, User: userName, Profile: profileName));
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ApplyDynamicPlan_Should_Reuse_Existing_Profile_And_Link_User()
    {
        var fake = new FakeUserManagerApi();
        // Pre-seed existing profile name according to helper naming: capGiB=0 => UL-<days>D-<users>U-<status>
        fake.Profiles.Add(new ProfileEntry("id1", "UL-30D-1U-active"));

        await fake.ApplyDynamicPlanAsync(
            user: "alice",
            password: "p@ss",
            days: 30,
            capGiB: 0,
            sharedUsers: 1,
            startMode: "assigned",
            rateLimit: null,
            staticIp: null,
            ct: default);

        fake.CreateProfileCalls.Should().Be(0, "should not create profile if it already exists");
        fake.LinkUserToProfileCalls.Should().Be(1, "should link user to existing profile");
        fake.UserProfiles.Should().ContainSingle(up => up.User == "alice" && up.Profile == "UL-30D-1U-active");
    }

    [Fact]
    public async Task ApplyDynamicPlan_Should_Not_Delete_Duplicate_ProfileLimitations()
    {
        var fake = new FakeUserManagerApi();
        // Use capGiB != 0 so limitationName will be generated
        int days = 60;
        int capGiB = 10;
        int sharedUsers = 1;
        string profileName = "10GB-60D-1U-active"; // expected profile name
        string limitationName = "10GB-60D";

        // Pre-seed duplicate profile->limitation links (simulate router having 5 identical links)
        for (int i = 0; i < 5; i++)
        {
            fake.ProfileLimitations.Add(new ProfileLimitationEntry(Id: $"*{i}", Profile: profileName, Limitation: limitationName));
        }

        // Call ApplyDynamicPlanAsync; it should NOT delete duplicates and also not create a new link
        await fake.ApplyDynamicPlanAsync(
            user: "bob",
            password: "p",
            days: days,
            capGiB: capGiB,
            sharedUsers: sharedUsers,
            startMode: "assigned",
            rateLimit: null,
            staticIp: null,
            ct: default);

        // Expect all existing duplicates to remain intact
        var matches = fake.ProfileLimitations.Where(pl => pl.Profile == profileName && pl.Limitation == limitationName).ToList();
        matches.Count.Should().Be(5, "ApplyDynamicPlanAsync should keep existing duplicate profile->limitation links and not delete them");

        // Also expect the profile to exist (created if missing) and the user linked to the profile
        fake.Profiles.Should().Contain(p => p.Name == profileName);
        fake.UserProfiles.Should().Contain(up => up.User == "bob" && up.Profile == profileName);
    }
}
