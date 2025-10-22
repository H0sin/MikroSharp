namespace MikroSharp.Abstractions;

/// <summary>
/// Entry point for interacting with MikroTik RouterOS REST endpoints using MikroSharp.
/// Create an instance of <c>MikroSharp.MikroSharpClient</c> to obtain implementations.
/// </summary>
public interface IMikroSharpClient
{
    /// <summary>
    /// User-Manager related operations (users, profiles, limitations, linking, etc.).
    /// </summary>
    IUserManagerApi UserManager { get; }
}