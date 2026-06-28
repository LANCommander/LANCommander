using System.Security.Claims;
using LANCommander.Server.Services;

namespace LANCommander.Server.Endpoints;

internal sealed class DownloadThrottle(UserService userService)
{
    /// <summary>
    /// Wraps the source stream in a <see cref="ThrottledStream"/> using the authenticated user's resolved download
    /// speed limit. Anonymous requests and users without a configured limit are returned unthrottled.
    /// </summary>
    public async Task<Stream> ApplyAsync(Stream source, ClaimsPrincipal? principal)
    {
        var userName = principal?.Identity?.Name;

        if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userName))
            return source;

        var user = await userService.GetAsync(userName);

        if (user == null)
            return source;

        var limits = await userService.GetLimitsAsync(user);

        if (limits.DownloadUnlimited)
            return source;

        return new ThrottledStream(source, limits.DownloadSpeedBytesPerSecond);
    }
}
