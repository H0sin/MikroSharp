using System.Text.Json.Serialization;

namespace MikroSharp.Models;

/// <summary>
/// Represents a User-Manager user entry.
/// </summary>
public record UmUser(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group")] string? Group,
    /// <summary>"yes"/"no" flag as returned by RouterOS.</summary>
    [property: JsonPropertyName("disabled")] string? Disabled,
    /// <summary>Returned as string by RouterOS (e.g., "1").</summary>
    [property: JsonPropertyName("shared-users")] string? SharedUsers, 
    /// <summary>Comma-separated RADIUS-like attributes (e.g., Mikrotik-Rate-Limit:...)</summary>
    [property: JsonPropertyName("attributes")] string? Attributes
);