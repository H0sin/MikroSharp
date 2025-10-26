using System.Globalization;
using System.Text.Json;
using MikroSharp.Models;

namespace MikroSharp.Endpoints;

public static class UserAccountExtensions
{
    public static UserAccountSummary ToSummary(this UserAccountStatus status, DateTimeOffset? now = null)
    {
        now ??= DateTimeOffset.UtcNow;

        var active = status.Profiles.FirstOrDefault(p => string.Equals(p.State, "running-active", StringComparison.OrdinalIgnoreCase))
                    ?? status.Profiles.FirstOrDefault();

        // Pick actual profile from monitor if available, else from active profile
        var actualProfile = status.Monitor?.ActualProfile ?? active?.Profile;

        // Parse end-time, RouterOS typically returns e.g. "2025-11-24 10:07:39"
        DateTimeOffset? endTime = ParseEndTime(active?.EndTime);

        // Remaining time
        TimeSpan? remaining = endTime.HasValue ? endTime.Value - now.Value : null;
        if (remaining.HasValue && remaining.Value < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        // Parse totals from monitor
        long? dl = ParseLong(status.Monitor?.TotalDownload);
        long? ul = ParseLong(status.Monitor?.TotalUpload);
        TimeSpan? uptime = ParseDuration(status.Monitor?.TotalUptime);

        return new UserAccountSummary(status.User, actualProfile, endTime, remaining, dl, ul, uptime);
    }

    private static DateTimeOffset? ParseEndTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // Try flexible parse first
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;

        // Try RouterOS common format "yyyy-MM-dd HH:mm:ss"
        if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return new DateTimeOffset(dt);

        return null;
    }

    private static long? ParseLong(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        return null;
    }

    private static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Expected like: 13h13m10s, maybe includes d (days) or w (weeks)
        // We'll scan tokens of <number><unit>
        int i = 0;
        long weeks = 0, days = 0, hours = 0, minutes = 0, seconds = 0;
        while (i < s.Length)
        {
            int start = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (start == i) break;
            if (!long.TryParse(s[start..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)) break;
            if (i >= s.Length) break;
            char unit = s[i++];
            switch (unit)
            {
                case 'w': weeks += num; break;
                case 'd': days += num; break;
                case 'h': hours += num; break;
                case 'm': minutes += num; break;
                case 's': seconds += num; break;
                default: return null;
            }
        }
        try
        {
            checked
            {
                var totalDays = (weeks * 7) + days;
                return new TimeSpan((int)totalDays, (int)hours, (int)minutes, (int)seconds);
            }
        }
        catch
        {
            return null;
        }
    }
}

