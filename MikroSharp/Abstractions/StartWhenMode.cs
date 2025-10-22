namespace MikroSharp.Abstractions;

/// <summary>
/// Profile start mode in RouterOS User-Manager.
/// </summary>
public enum StartWhenMode
{
    Assigned,
    FirstAuth
}

internal static class StartWhenModeExtensions
{
    public static string ToApiValue(this StartWhenMode mode) => mode switch
    {
        StartWhenMode.Assigned => "assigned",
        StartWhenMode.FirstAuth => "first-auth",
        _ => "assigned"
    };
}

