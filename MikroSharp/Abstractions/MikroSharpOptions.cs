using System;
using System.Collections.Generic;

namespace MikroSharp.Abstractions;

/// <summary>
/// Configuration options for creating a MikroSharpClient instance.
/// </summary>
public sealed class MikroSharpOptions
{
    /// <summary>
    /// Router base URL, e.g. "https://192.168.88.1" (no trailing slash required).
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Username used for basic authentication.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Password used for basic authentication.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Optional HttpClient timeout. If null, HttpClient default is used.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Optional additional default headers to include with every request.
    /// </summary>
    public IDictionary<string, string>? DefaultHeaders { get; init; }

    /// <summary>
    /// Whether the MikroSharpClient should dispose the underlying HttpClient when disposed. Default: true.
    /// </summary>
    public bool DisposeHttpClient { get; init; } = true;
}
