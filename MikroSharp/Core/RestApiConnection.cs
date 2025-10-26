using System.Text;
using System.Text.Json;

namespace MikroSharp.Core;

internal class RestApiConnection(HttpClient http, JsonSerializerOptions jsonOptions, bool disposeClient = false)
    : IApiConnection
{
    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, path, null, ct);
        var contentStream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(contentStream, jsonOptions, ct)
               ?? throw new MikroSharpException("Failed to deserialize response.", "GET", path, response.StatusCode,
                   null);
    }

    public async Task PutAsync(string path, object body, CancellationToken ct = default)
    {
        using var content = SerializeBody(body);
        using var response = await SendCoreAsync(HttpMethod.Put, path, content, ct);
    }

    public async Task PatchAsync(string path, object body, CancellationToken ct = default)
    {
        using var content = SerializeBody(body);
        using var response = await SendCoreAsync(HttpMethod.Patch, path, content, ct);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Delete, path, null, ct);
    }

    public async Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var content = SerializeBody(body);
        using var response = await SendCoreAsync(HttpMethod.Post, path, content, ct);
        var contentStream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(contentStream, jsonOptions, ct)
               ?? throw new MikroSharpException("Failed to deserialize response.", "POST", path, response.StatusCode, null);
    }

    public async Task PostAsync(string path, object body, CancellationToken ct = default)
    {
        using var content = SerializeBody(body);
        using var response = await SendCoreAsync(HttpMethod.Post, path, content, ct);
    }

    private StringContent SerializeBody(object body)
    {
        string json = JsonSerializer.Serialize(body, jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, HttpContent? content,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path) { Content = content };

        try
        {
            var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                string errorBody = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                string detail = errorBody;
                try
                {
                    using var doc = JsonDocument.Parse(errorBody);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("detail", out var d))
                        {
                            detail = d.GetString() ?? errorBody;
                        }
                        else if (root.TryGetProperty("message", out var m))
                        {
                            detail = m.GetString() ?? errorBody;
                        }
                    }
                }
                catch
                {
                    // leave detail as raw error body if not JSON
                }
                throw new MikroSharpException($"HTTP Error: {detail}", method.Method, path, res.StatusCode, errorBody);
            }

            return res;
        }
        catch (MikroSharpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MikroSharpException($"Network or request error: {ex.Message}", method.Method, path, null, null,
                ex);
        }
    }

    public void Dispose()
    {
        if (disposeClient)
        {
            http.Dispose();
        }
    }
}