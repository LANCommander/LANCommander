using Microsoft.AspNetCore.Http;

namespace LANCommander.Server.Settings.Models;

public class AuthenticationSettings
{
    public bool RequireApproval { get; set; } = false;
    public string TokenSecret { get; set; } = Guid.NewGuid().ToString();
    public int TokenLifetime { get; set; } = 30;
    public bool PasswordRequireNonAlphanumeric { get; set; } = false;
    public bool PasswordRequireLowercase { get; set; } = false;
    public bool PasswordRequireUppercase { get; set; } = false;
    public bool PasswordRequireDigit { get; set; } = true;
    public int PasswordRequiredLength { get; set; } = 8;

    public CookiePolicy HttpCookiePolicy { get; set; } = new()
    {
        SameSite = SameSiteMode.Lax,
        Secure = CookieSecurePolicy.None
    };

    public CookiePolicy HttpsCookiePolicy { get; set; } = new()
    {
        SameSite = SameSiteMode.None,
        Secure = CookieSecurePolicy.SameAsRequest
    };
        
    public IEnumerable<AuthenticationProvider> AuthenticationProviders { get; set; } = new List<AuthenticationProvider>();
}