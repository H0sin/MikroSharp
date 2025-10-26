// filepath: /home/h0sin/dotnet/MikroSharp/MikroSharp/Models/UserAccountStatus.cs
namespace MikroSharp.Models;

/// <summary>
/// Combined account status for a user: current profile links with state/expiry, and monitor counters.
/// </summary>
public record UserAccountStatus(
    string User,
    List<UserProfileStatus> Profiles,
    UserMonitorInfo? Monitor
);

