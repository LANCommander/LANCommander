using System.Net;
using System.Net.Http.Headers;
using LANCommander.HQ.SDK.Authentication;

namespace LANCommander.Server.Services.HQ.Registry;

/// <summary>
/// Signs requests to the HQ package registry with this server's HQ session, the same access token
/// the SDK sends to the rest of HQ, so no personal access token has to be created for the registry.
/// </summary>
/// <remarks>
/// The token comes from the server's one <see cref="RefreshingTokenProvider"/>. Never build a second
/// provider for the registry: refresh tokens are single-use, and two providers rotating the same one
/// get the whole session revoked.
/// </remarks>
public sealed class HqRegistryAuthenticationHandler(
    ITokenProvider tokens,
    HqConnectionService connection) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            await AuthorizeAsync(request, cancellationToken);

            response = await base.SendAsync(request, cancellationToken);

            // The cached access token can be refused before the time it says it expires, e.g. when HQ
            // restarted with a new signing key. Renew it once; a request with a body can't be sent twice.
            if (response.StatusCode == HttpStatusCode.Unauthorized
                && request.Content == null
                && tokens is IInvalidatableTokenProvider invalidatable)
            {
                response.Dispose();

                await invalidatable.InvalidateAsync(cancellationToken);
                await AuthorizeAsync(request, cancellationToken);

                response = await base.SendAsync(request, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (ex is not HqRegistryUnavailableException)
                connection.ReportFailure(ex);

            throw;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized)
            connection.ReportFailure(new HttpRequestException("LANCommander HQ refused this server's session.", null, response.StatusCode));
        else if ((int)response.StatusCode < 500)
            connection.ReportSuccess();

        return response;
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
            throw new HqRegistryUnavailableException();

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

/// <summary>Thrown when the registry is used while this server has no HQ session to sign in with.</summary>
public sealed class HqRegistryUnavailableException()
    : InvalidOperationException("This server isn't connected to LANCommander HQ. Connect it in Settings to use the HQ registry.");
