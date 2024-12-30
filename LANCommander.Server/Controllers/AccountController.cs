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

    [HttpGet("/AccountLink")]
    public async Task<IActionResult> AccountLink(Guid code, string returnUrl = "/")
    {
        var payload = await Cache.GetOrDefaultAsync<AccountLinkPayload>($"AccountLink/{code}", null);
        
        if (payload == null)
            return Unauthorized();

        var provider =
            Settings.Authentication.AuthenticationProviders.FirstOrDefault(ap =>
                ap.Slug == payload.AuthenticationProviderSlug);

        var idClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (idClaim == null)
            return Unauthorized();

        await UserCustomFieldService.AddAsync(new UserCustomField
        {
            UserId = payload.UserId,
            Name = provider.GetCustomFieldName(),
            Value =  idClaim.Value,
        });
        
        return Redirect(returnUrl);
    }
    
    [HttpPost("/AccountLink")]
    public async Task<IActionResult> AccountLink(string providerSlug, string returnUrl = "/")
    {
        var user = await UserService.GetAsync(User.Identity.Name);
        var provider = Settings.Authentication.AuthenticationProviders.FirstOrDefault(ap => ap.Slug == providerSlug);
        var code = Guid.NewGuid();

        await Cache.SetAsync($"AccountLink/{code}", new AccountLinkPayload
        {
            Code = code,
            UserId = user.Id,
            AuthenticationProviderSlug = provider.Slug
        }, TimeSpan.FromMinutes(5));

        var queryBuilder = new QueryBuilder();

        var redemptionUrl = Url.Action("AccountLink", new { code = code, returnUrl = returnUrl });
        
        return Challenge(new AuthenticationProperties { RedirectUri = redemptionUrl }, provider.Slug);
    }
}