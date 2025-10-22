using System.Text.Json.Serialization;

namespace MikroSharp.Models;

public record LimitationEntry(
    [property: JsonPropertyName(".id")] string Id,
    [property: JsonPropertyName("name")] string Name
);