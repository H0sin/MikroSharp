using System.Collections.Generic;
using System.Text.Json;

namespace MikroSharp.Models;

public record UserProfileDetails(
    string Profile,
    List<string> Limitations
);

public record UserDetails(
    UmUser User,
    List<UserProfileDetails> Profiles,
    string? RateLimit,
    string? StaticIp,
    int? SessionTimeout,
    UserMonitorInfo? Monitor
);