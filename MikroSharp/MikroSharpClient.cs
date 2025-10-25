using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MikroSharp.Abstractions;
using MikroSharp.Core;
using MikroSharp.Endpoints;

namespace MikroSharp;

public class MikroSharpClient : IMikroSharpClient, IDisposable
{
    private readonly IApiConnection _connection;
    private readonly bool _disposeConnection;

    // Primary API surface
    public IUserManagerApi UserManager { get; }

    /// <summary>
    /// Create a new client instance using router base URL and basic-auth credentials.
    /// </summary>
    /// <param name="baseUrl">Router base URL, e.g. "https://192.168.88.1"</param>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <param name="handler">Optional custom HttpMessageHandler (e.g., for tests or custom SSL).</param>
    public MikroSharpClient(string baseUrl, string username, string password, HttpMessageHandler? handler = null)
    {
        var httpClient = handler == null ? new HttpClient() : new HttpClient(handler, false);
        httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"))
        );
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
        };

        _connection = new RestApiConnection(httpClient, jsonOptions, disposeClient: true);
        _disposeConnection = true;

        UserManager = new UserManagerApi(_connection);
    }

    /// <summary>
    /// Create a client using options and optional JSON configuration.
    /// </summary>
    public MikroSharpClient(MikroSharpOptions options, HttpMessageHandler? handler = null,
        Action<JsonSerializerOptions>? configureJson = null)
    {
        var httpClient = handler == null ? new HttpClient() : new HttpClient(handler, false);
        httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}"))
        );
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (options.Timeout.HasValue)
            httpClient.Timeout = options.Timeout.Value;
        if (options.DefaultHeaders != null)
        {
            foreach (var kv in options.DefaultHeaders)
            {
                httpClient.DefaultRequestHeaders.Remove(kv.Key);
                httpClient.DefaultRequestHeaders.Add(kv.Key, kv.Value);
            }
        }

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
        };
        configureJson?.Invoke(jsonOptions);

        _connection = new RestApiConnection(httpClient, jsonOptions, disposeClient: options.DisposeHttpClient);
        _disposeConnection = options.DisposeHttpClient;

        UserManager = new UserManagerApi(_connection);
    }

    /// <summary>
    /// Create a client that ignores SSL certificate validation (ONLY for development/testing in lab environments).
    /// </summary>
    public static MikroSharpClient CreateInsecure(string baseUrl, string username, string password)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new MikroSharpClient(baseUrl, username, password, handler);
    }

    public void Dispose()
    {
        if (_disposeConnection)
        {
            _connection.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}