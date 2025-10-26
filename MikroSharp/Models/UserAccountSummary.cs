namespace MikroSharp.Models;

/// <summary>
/// Parsed/derived view of a user's account status for quick consumption in apps.
/// </summary>
public record UserAccountSummary(
    string User,
    string? ActualProfile,
    DateTimeOffset? EndTime,
    TimeSpan? Remaining,
    long? TotalDownloadBytes,
    long? TotalUploadBytes,
    TimeSpan? TotalUptime
);

