namespace MikroSharp.Core;

public interface IApiConnection : IDisposable
{
    Task<T> GetAsync<T>(string path, CancellationToken ct = default);
    Task PutAsync(string path, object body, CancellationToken ct = default);
    Task PatchAsync(string path, object body, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}