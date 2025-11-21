using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using Action = System.Action;

namespace LANCommander.SDK.Helpers;

public class ApiRequestBuilder(
    HttpClient httpClient,
    ITokenProvider tokenProvider,
    ISettingsProvider settingsProvider)
{
    private AuthToken _token { get; set; } = tokenProvider.GetToken();
    private bool _ignoreVersion { get; set; }
    private object _body { get; set; }
    private string _route { get; set; }
    private HttpClient _httpClient { get; set; } = httpClient;
    private HttpRequestMessage _request { get; set; } = new();
    private CancellationToken  _cancellationToken { get; set; } = CancellationToken.None;
    private Action<DownloadProgressChangedEventArgs> _progressHandler { get; set; }
    private Action _completeHandler { get; set; }
    private Uri _baseAddress { get; set; } = settingsProvider.CurrentValue.Authentication.ServerAddress;

    private async ValueTask<TResult> DeserializeResultAsync<TResult>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(_cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<TResult>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return default;
        }
    }

    public ApiRequestBuilder UseAuthenticationToken()
    {
        _request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);

        return this;
    }

    public ApiRequestBuilder UseRoute(string route)
    {
        _request.RequestUri = _baseAddress.Join(route);

        return this;
    }

    public ApiRequestBuilder UseCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        return this;
    }

    public ApiRequestBuilder UseVersioning()
    {
        _request.Headers.Add("X-API-Version", VersionHelper.GetCurrentVersion().ToString());
        
        // _request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

        return this;
    }

    public ApiRequestBuilder UseMethod(HttpMethod method)
    {
        _request.Method = method;

        return this;
    }

    public ApiRequestBuilder UseBaseAddress(Uri baseAddress)
    {
        _baseAddress = baseAddress;

        return this;
    }

    public ApiRequestBuilder SetTimeout(TimeSpan timeout)
    {
        _httpClient.Timeout = timeout;

        return this;
    }

    public ApiRequestBuilder AddBody(object body)
    {
        _request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, MediaTypeNames.Application.Json);

        return this;
    }

    public ApiRequestBuilder AddHeader(string key, string value)
    {
        _request.Headers.Add(key, value);

        return this;
    }

    public ApiRequestBuilder OnProgress(Action<DownloadProgressChangedEventArgs> progressHandler)
    {
        _progressHandler = progressHandler;

        return this;
    }

    public ApiRequestBuilder OnComplete(Action completeHandler)
    {
        _completeHandler = completeHandler;

        return this;
    }

    public async Task<ApiResponseMessage<TResult>> SendAsync<TResult>() where TResult : class
    {
        var response = await _httpClient.SendAsync(_request, _cancellationToken);

        var result = new ApiResponseMessage<TResult>
        {
            Response = response,
            Data = await DeserializeResultAsync<TResult>(response)
        };
        
        return result;
    }

    public async Task<TResult> GetAsync<TResult>()
    {
        _request.Method = HttpMethod.Get;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);
        
        response
            .EnsureSuccessStatusCode();

        return await DeserializeResultAsync<TResult>(response);
    }

    public async Task<TResult> PostAsync<TResult>()
    {
        _request.Method = HttpMethod.Post;

        var response = await _httpClient.SendAsync(_request, _cancellationToken);

        response
            .EnsureSuccessStatusCode();

        return await DeserializeResultAsync<TResult>(response);
    }

    public async Task PostAsync()
    {
        _request.Method = HttpMethod.Post;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);
        
        response
            .EnsureSuccessStatusCode();
    }

    public async Task<TResult> PutAsync<TResult>()
    {
        _request.Method = HttpMethod.Put;
        
        var response = await  _httpClient.SendAsync(_request, _cancellationToken);
        
        response
            .EnsureSuccessStatusCode();

        return await DeserializeResultAsync<TResult>(response);
    }

    public async Task<TResult> DeleteAsync<TResult>()
    {
        _request.Method = HttpMethod.Delete;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);

        response
            .EnsureSuccessStatusCode();
        
        return await DeserializeResultAsync<TResult>(response);
    }

    public async Task HeadAsync()
    {
        _request.Method = HttpMethod.Head;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);

        response
            .EnsureSuccessStatusCode();
    }

    public async Task<TResult> HeadAsync<TResult>()
    {
        _request.Method = HttpMethod.Head;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);
        
        response
            .EnsureSuccessStatusCode();
        
        return await DeserializeResultAsync<TResult>(response);
    }

    public async Task<FileInfo> DownloadAsync(string destination)
    {
        _request.Method = HttpMethod.Get;
        
        var response = await _httpClient.SendAsync(_request, _cancellationToken);

        await using (var fs = new FileStream(destination, FileMode.Create))
        {
            var responseStream = await response.Content.ReadAsTrackableStreamAsync(_cancellationToken);

            responseStream.OnProgress += (position, length) =>
            {
                if (_progressHandler != null)
                    _progressHandler.Invoke(new DownloadProgressChangedEventArgs
                    {
                        BytesReceived = position,
                        TotalBytes = length,
                    });
            };

            await responseStream.CopyToAsync(fs, _cancellationToken);

            if (_completeHandler != null)
                _completeHandler();
        }

        return new FileInfo(destination);
    }

    public async Task<TrackableStream> StreamAsync()
    {
        _request.Method = HttpMethod.Get;
        
        var response = await _httpClient.SendAsync(_request, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
        
        return await response.Content.ReadAsTrackableStreamAsync(_cancellationToken);
    }

    public async Task<TResult> UploadAsync<TResult>(string fileName, byte[] data)
    {
        using (var form = new MultipartFormDataContent())
        {
            var dataContent = new ByteArrayContent(data);
            
            form.Add(dataContent, "file", fileName);

            _request.Content = form;
            _request.Method = HttpMethod.Post;
            
            var response = await _httpClient.SendAsync(_request, _cancellationToken);

            return await DeserializeResultAsync<TResult>(response);
        }
    }

    public async Task<TResult> UploadAsync<TResult>(string fileName, Stream data)
    {
        var buffer = new byte[data.Length];

        await data.ReadExactlyAsync(buffer, 0, buffer.Length, _cancellationToken);
        
        return await UploadAsync<TResult>(fileName, buffer);
    }

    public async Task<Guid> UploadInChunksAsync(long chunkSize, Stream data)
    {
        try
        {
            var initResponse = await new ApiRequestBuilder(httpClient, tokenProvider, settingsProvider)
                .UseRoute("/Upload/Init")
                .UseVersioning()
                .UseAuthenticationToken()
                .UseCancellationToken(_cancellationToken)
                .PostAsync<UploadInitResponse>();

            var buffer = new byte[chunkSize];

            while (data.Position < data.Length)
            {
                var start = data.Position;

                if (data.Position + chunkSize > data.Length)
                {
                    var remainingBytes = data.Length - data.Position;

                    buffer = new byte[remainingBytes];
                }

                await data.ReadExactlyAsync(buffer, 0, buffer.Length, _cancellationToken);

                var chunkRequest = new UploadChunkRequest
                {
                    Start = start,
                    End = data.Position,
                    File = buffer,
                    Key = initResponse.Key,
                };

                await new ApiRequestBuilder(httpClient, tokenProvider, settingsProvider)
                    .AddBody(chunkRequest)
                    .UseRoute("/Upload/Chunk")
                    .UseVersioning()
                    .UseAuthenticationToken()
                    .UseCancellationToken(_cancellationToken)
                    .PostAsync();
            }

            return initResponse.Key;
        }
        catch (Exception ex)
        {
            return default;
        }
    } 
}