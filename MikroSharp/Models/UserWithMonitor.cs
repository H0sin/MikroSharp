using System.Text.Json;

namespace MikroSharp.Models;

public record UserWithMonitor(
    UmUser User,
    JsonElement Monitor
);

