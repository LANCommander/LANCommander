namespace LANCommander.Server.Settings.Models;

/// <summary>
/// Credentials for PCGamingWiki's MediaWiki API.
/// <para>
/// These are optional. Without them the metadata provider falls back to the anonymous
/// endpoints (<c>opensearch</c>/<c>parse</c>) and scrapes the rendered page. Since the
/// August 2026 server migration the <c>cargoquery</c> endpoint, which returns structured
/// data instead, refuses anonymous callers.
/// </para>
/// </summary>
public class PcGamingWikiSettings
{
    /// <summary>
    /// Bot password username, in <c>Account@BotName</c> form.
    /// </summary>
    public string Username { get; set; } = String.Empty;

    /// <summary>
    /// The generated password from Special:BotPasswords. The bot password must be granted the
    /// "Create, query and delete data through the Cargo extension" grant.
    /// </summary>
    public string BotPassword { get; set; } = String.Empty;
}
