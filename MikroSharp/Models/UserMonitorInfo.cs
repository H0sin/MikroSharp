using System.Text.Json.Serialization;

namespace MikroSharp.Models;

public record UserMonitorInfo(
    [property: JsonPropertyName("active-sessions")] string? ActiveSessions,
    [property: JsonPropertyName("active-sub-sessions")] string? ActiveSubSessions,
    [property: JsonPropertyName("actual-profile")] string? ActualProfile,
    [property: JsonPropertyName("attributes-details")] string? AttributesDetails,
    [property: JsonPropertyName("total-download")] string? TotalDownload,
    [property: JsonPropertyName("total-upload")] string? TotalUpload,
    [property: JsonPropertyName("total-uptime")] string? TotalUptime
);
