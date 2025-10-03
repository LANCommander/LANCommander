using System;
using System.Net.Http;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.Factories;

public class ApiRequestFactory(
    HttpClient httpClient,
    ITokenProvider tokenProvider,
    ISettingsProvider settingsProvider)
{
    public ApiRequestBuilder Create()
    {
        return new ApiRequestBuilder(httpClient, tokenProvider, settingsProvider);
    }
}