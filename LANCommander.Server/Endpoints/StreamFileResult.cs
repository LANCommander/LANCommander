using System.Net.Mime;
using Microsoft.AspNetCore.Http;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// Streams a file to the response with a configurable buffer size for higher download throughput.
/// </summary>
internal sealed class StreamFileResult : IResult
{
    private const int DefaultBufferSize = 1024 * 1024; // 1 MB

    private readonly Stream _fileStream;
    private readonly string _contentType;
    private readonly string _fileDownloadName;
    private readonly int _bufferSize;

    public StreamFileResult(
        Stream fileStream,
        string contentType,
        string fileDownloadName,
        int bufferSize = DefaultBufferSize)
    {
        _fileStream = fileStream;
        _contentType = contentType;
        _fileDownloadName = fileDownloadName;
        _bufferSize = bufferSize;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        var cancellationToken = httpContext.RequestAborted;

        response.ContentType = _contentType;
        response.Headers.ContentLength = _fileStream.Length;
        response.Headers.ContentDisposition = $"attachment; filename=\"{_fileDownloadName}\"";

        try
        {
            await _fileStream.CopyToAsync(response.Body, _bufferSize, cancellationToken);
        }
        finally
        {
            await _fileStream.DisposeAsync();
        }
    }
}
