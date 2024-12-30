using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers;

public class AccountController : BaseController
{
    public AccountController(ILogger<AccountController> logger) : base(logger)
    {
    }

    [HttpGet("/SignInOAuth")]
    public async Task<IActionResult> SignInOAuth()
    {
        var authenticationProviders = await AuthenticationService.GetAuthenticationProviderTemplatesAsync();

        return Ok();
    }
}