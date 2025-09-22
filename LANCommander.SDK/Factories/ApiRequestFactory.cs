using System.Net.Http;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Factories;

public class ApiRequestFactory(HttpClient httpClient, ITokenProvider tokenProvider, ILANCommanderConfiguration config)
{
    public ApiRequestBuilder Create()
    {
        return new ApiRequestBuilder(httpClient, tokenProvider, config);
    }
}