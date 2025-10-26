// filepath: /home/h0sin/dotnet/MikroSharp/MikroSharp/Models/UserProfileStatus.cs
using System.Text.Json.Serialization;

namespace MikroSharp.Models;

/// <summary>
/// A user->profile link entry as returned by GET /rest/user-manager/user-profile?user={name}
/// Includes runtime state and end-time when available.
/// </summary>
public record UserProfileStatus(
    [property: JsonPropertyName(".id")] string Id,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("end-time")] string? EndTime
);

