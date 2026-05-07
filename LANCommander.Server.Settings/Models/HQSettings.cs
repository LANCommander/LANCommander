namespace LANCommander.Server.Settings.Models;

public class HQSettings
{
    public string BaseUrl { get; set; } = "https://api.lancommander.app";
    public string AccessToken { get; set; } = string.Empty;
    public DateTime? TokenExpiresAt { get; set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        (!TokenExpiresAt.HasValue || TokenExpiresAt.Value >= DateTime.UtcNow);
}
