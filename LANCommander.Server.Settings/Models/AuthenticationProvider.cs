using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class AuthenticationProvider
{
    public string Name { get; set; }
    public string Slug { get; set; }
    public AuthenticationProviderType Type { get; set; } = AuthenticationProviderType.OAuth2;
    public string Color { get; set; }
    public string Icon { get; set; }
    public string Documentation { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string AuthorizationEndpoint { get; set; }
    public string TokenEndpoint { get; set; }
    public string UserInfoEndpoint { get; set; }
    public string ConfigurationUrl { get; set; }
    public IEnumerable<string> Scopes { get; set; } = new List<string>();
    public IEnumerable<ClaimMapping> ClaimMappings { get; set; } = new List<ClaimMapping>();
        
    public string GetCustomFieldName()
    {
        return $"ExternalId/{Slug}";
    }
}