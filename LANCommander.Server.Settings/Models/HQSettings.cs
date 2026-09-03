using YamlDotNet.Serialization;

namespace LANCommander.Server.Settings.Models;

/// <summary>
/// Persisted LANCommander HQ credential and polling configuration.
///
/// This holds the credential ONLY. Whether that credential currently works is runtime state and
/// lives in HqConnectionService — it is deliberately not persisted, because a token that was valid
/// when it was written to disk may have expired, been revoked, or become unreachable since.
///
/// The token fields are owned by SettingsHqTokenStore and rewritten by the HQ SDK on every refresh.
/// Do not set them from anywhere else: refresh tokens are single-use, so an out-of-band write can
/// strand the server on a credential the server-side session has already retired.
/// </summary>
public class HQSettings
{
    public string BaseUrl { get; set; } = "https://api.lancommander.app";

    /// <summary>Short-lived token actually sent with requests. Renewed automatically by the SDK.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public DateTime? AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Long-lived credential the SDK uses to mint access tokens. This is the value that keeps the
    /// server connected across restarts and idle periods, and it rotates on every use.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the refresh token lapses through inactivity. The clock resets on every use, so a server
    /// that talks to HQ even occasionally never reaches it.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Label recorded against the HQ session so a user can recognise this server on their account
    /// page and revoke the right one. Defaults to the machine name when left blank.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Seconds between background re-verification probes against HQ.</summary>
    public int VerifyIntervalSeconds { get; set; } = 900;

    /// <summary>
    /// Whether a credential is stored at all. This answers "should we offer Connect or Unlink?",
    /// NOT "is HQ usable right now?" — for the latter, ask HqConnectionService.
    /// </summary>
    /// <remarks>
    /// <see cref="YamlIgnoreAttribute"/> is required: the default SerializerBuilder in
    /// YamlSerializerFactory emits get-only properties, which previously wrote a dead
    /// <c>IsAuthenticated: true</c> line into Settings.yml that ConfigurationBinder could never
    /// read back.
    /// </remarks>
    [YamlIgnore]
    public bool HasCredential =>
        !string.IsNullOrWhiteSpace(RefreshToken) || !string.IsNullOrWhiteSpace(AccessToken);
}
