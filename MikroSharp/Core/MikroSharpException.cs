using System.Net;

namespace MikroSharp.Core;

public class MikroSharpException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }
    public string Path { get; }
    public string Method { get; }

    public MikroSharpException(string message, string method, string path, HttpStatusCode? statusCode, string? responseBody, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Path = path;
        Method = method;
    }

    public override string Message => 
        $"{base.Message} (Status: {StatusCode}, Method: {Method}, Path: {Path})";
}