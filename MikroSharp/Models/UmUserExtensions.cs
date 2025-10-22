using System;

namespace MikroSharp.Models;

/// <summary>
/// Convenience helpers for working with <see cref="UmUser"/> instances.
/// </summary>
public static class UmUserExtensions
{
    /// <summary>
    /// Returns true if the user is disabled (Disabled == "yes"), false if "no" or null.
    /// </summary>
    public static bool IsDisabled(this UmUser user) => string.Equals(user.Disabled, "yes", StringComparison.OrdinalIgnoreCase);
 
    /// <summary>
    /// Tries to parse the SharedUsers string to an integer; returns null if not available or invalid.
    /// </summary>
    public static int? SharedUsersAsInt(this UmUser user)
        => int.TryParse(user.SharedUsers, out var i) ? i : null;
}
