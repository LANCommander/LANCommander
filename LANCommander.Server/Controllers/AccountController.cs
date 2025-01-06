using System.Security.Claims;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;
using AuthenticationService = LANCommander.Server.Services.AuthenticationService;

namespace LANCommander.Server.Controllers;

public class AccountController : BaseController
{
    private readonly UserService UserService;
    private readonly UserCustomFieldService UserCustomFieldService;
    private readonly IFusionCache Cache;
    
    public AccountController(
        UserService userService,
        UserCustomFieldService userCustomFieldService,
        IFusionCache cache,
        ILogger<AccountController> logger) : base(logger)
    {
        UserService = userService;
        UserCustomFieldService = userCustomFieldService;
        Cache = cache;
    }

    [HttpGet("/SignInOAuth")]
    public async Task<IActionResult> SignInOAuth()
    {
        var authenticationProviders = await AuthenticationService.GetAuthenticationProviderTemplatesAsync();

        return Ok();
    }
    
    [HttpPost("/AccountLink")]
    public async Task<IActionResult> AccountLink(string providerSlug, string returnUrl = "/")
    {
        var user = await UserService.GetAsync(User.Identity.Name);
        var provider = Settings.Authentication.AuthenticationProviders.FirstOrDefault(ap => ap.Slug == providerSlug);

        var items = new Dictionary<string, string>()
        {
            { "UserId", user.Id.ToString() },
            { "Action", AuthenticationProviderActionType.AccountLink },
        };
        
        return Challenge(new AuthenticationProperties(items) { RedirectUri = returnUrl, AllowRefresh = true }, provider.Slug);
    }

    [HttpPost("/RegisterByAuthenticationProvider")]
    public async Task<IActionResult> RegisterByAuthenticationProvider(string provider, string returnUrl = "/")
    {
        if (!String.IsNullOrWhiteSpace(provider))
        {
            var properties = new AuthenticationProperties(new Dictionary<string, string>()
            {
                { "Action", AuthenticationProviderActionType.Register }
            });

            properties.RedirectUri = returnUrl;
                
            return Challenge(properties, provider);
        }
        else
        {
            return BadRequest();
        }
    }
}