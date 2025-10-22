using System.Text.Json.Serialization;

namespace MikroSharp.Models;

public record UmUserProfile(
    [property: JsonPropertyName(".id")] string Id,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("profile")] string Profile
);