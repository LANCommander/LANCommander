namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Where the browser finds a user's avatar. Relative, since the page is served by the same server;
/// building an absolute address would need the HTTP request, which a circuit doesn't reliably have.
/// </summary>
public static class ChatAvatar
{
    /// <param name="userName">The user's username (not their alias, which avatars aren't keyed by)</param>
    public static string? Url(string? userName)
        => string.IsNullOrWhiteSpace(userName) ? null : $"/api/Profile/{Uri.EscapeDataString(userName)}/Avatar";
}
