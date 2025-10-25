using System.Text.Json.Serialization;

namespace MikroSharp.Models;

public record ProfileLimitationEntry(
    [property: JsonPropertyName(".id")] string Id,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("limitation")] string Limitation
);

