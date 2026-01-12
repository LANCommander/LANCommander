using LANCommander.SDK.Abstractions;
using Microsoft.AspNetCore.Http;

namespace LANCommander.Server.Services.Providers;

public class ServerAddressProvider(IHttpContextAccessor httpContextAccessor) : IServerAddressProvider
{
    public void SetServerAddress(Uri serverAddress)
    {
        // Do nothing
    }

    public Uri GetServerAddress()
    {
        var request = httpContextAccessor.HttpContext.Request;

        return new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Port = request.Host.Port ?? -1,
            Path = request.PathBase
        }.Uri;
    }
}