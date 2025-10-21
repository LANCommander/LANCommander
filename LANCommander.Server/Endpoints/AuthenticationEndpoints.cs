using System.Security.Claims;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/Logout", LogoutAsync);
        routes.MapPost("/SignInWeb", SignInWebAsync).AllowAnonymous();
        routes.MapGet("/AccountLink", AccountLinkAsync);
        routes.MapPost("/RegisterByAuthenticationProvider", RegisterByAuthenticationProvider);
    }
    
    public static async Task<IResult> LogoutAsync(
        [FromServices] SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();

        return TypedResults.Redirect("/Login");
    }

    public static async Task<IResult> SignInWebAsync(
        LoginModel model,
        [FromServices] SignInManager<User> signInManager,
        [FromServices] UserService userService)
    {
        var user = await userService.GetAsync(model.Username);

        var result = await signInManager.CheckPasswordSignInAsync(user, model.Password, false);

        if (!result.Succeeded)
            return TypedResults.Unauthorized();
        
        return TypedResults.Redirect("/");
    }

    public static async Task<IResult> AccountLinkAsync(
        [FromServices] UserService userService,
        ClaimsPrincipal userPrincipal,
        string provider,
        string returnUrl = "/")
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? "");

        var items = new Dictionary<string, string>
        {
            { "UserId", user.Id.ToString() },
            { "Action", AuthenticationProviderActionType.AccountLink },
        };

        return TypedResults.Challenge(new AuthenticationProperties(items)
        {
            RedirectUri = returnUrl,
            AllowRefresh = true,
        }, [provider]);
    }

    public static IResult RegisterByAuthenticationProvider(
        string provider,
        string returnUrl = "/")
    {
        if (!String.IsNullOrWhiteSpace(provider))
        {
            var properties = new AuthenticationProperties(new Dictionary<string, string>
            {
                { "Action", AuthenticationProviderActionType.Register }
            });
            
            properties.RedirectUri = returnUrl;
            
            return TypedResults.Challenge(properties, new List<string> { provider });
        }
        
        return TypedResults.BadRequest();
    }
}