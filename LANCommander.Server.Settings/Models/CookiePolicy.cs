using Microsoft.AspNetCore.Http;

namespace LANCommander.Server.Settings.Models;

public class CookiePolicy
{
    public SameSiteMode SameSite { get; set; } = SameSiteMode.None;
    public CookieSecurePolicy Secure { get; set; } = CookieSecurePolicy.SameAsRequest;
}