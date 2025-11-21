using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using AuthenticationService = LANCommander.Server.Services.AuthenticationService;

namespace LANCommander.Server.Endpoints;

public static class AuthApiEndpoints
{
    public static void MapAuthApiEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Auth");

        group.MapPost("/Login", LoginAsync);
        group.MapGet("/Login", LoginByProviderAsync);
        group.MapPost("/Logout", LogoutAsync);
        group.MapPost("/Validate", ValidateAsync).RequireAuthorization();
        group.MapPost("/Refresh", RefreshAsync);
        group.MapPost("/Register", RegisterAsync);
        group.MapGet("/AuthenticationProviders", GetAuthenticationProvidersAsync);
    }

    internal static async Task<IResult> LoginAsync(
        [FromBody] LoginModel model,
        [FromServices] AuthenticationService authenticationService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AuthApi");

        try
        {
            var token = await authenticationService.LoginAsync(model.Username, model.Password);

            logger.LogDebug("Successfully logged in user {UserName}", model.Username);

            return TypedResults.Ok(token);
        }
        catch (UserAuthenticationException ex)
        {
            List<ErrorResponse.ErrorInfo> errorDetails =
                ex.IdentityResult?.Errors.Select(FromIdentityResult).ToList() ?? [];

            if (errorDetails.Count == 0)
            {
                errorDetails.Add(new ErrorResponse.ErrorInfo { Message = ex.Message });
            }

            var errorResponse = new ErrorResponse
            {
                Error = "AuthenticationFailed",
                Message = "User authentication failed.",
                Details = errorDetails,
            };
            
            return TypedResults.BadRequest(errorResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while trying to log in {UserName}", model.Username);

            return TypedResults.BadRequest(ex.Message);
        }
    }
    
    internal static async Task<IResult> LoginByProviderAsync(
        HttpContext httpContext,
        [FromServices] AuthenticationService authenticationService,
        [FromServices] IFusionCache cache,
        string provider = "")
    {
        var user = httpContext.User;

        if (!string.IsNullOrWhiteSpace(provider) && !(user?.Identity?.IsAuthenticated ?? false))
        {
            var properties = new AuthenticationProperties(new Dictionary<string, string>
            {
                { "Action", AuthenticationProviderActionType.Login }
            })
            {
                RedirectUri = $"/api/Auth/Login?provider={Uri.EscapeDataString(provider)}"
            };

            return TypedResults.Challenge(properties, [provider]);
        }

        if (!string.IsNullOrWhiteSpace(provider) && (user?.Identity?.IsAuthenticated ?? false))
        {
            var code = Guid.NewGuid().ToString();
            var token = await authenticationService.LoginAsync(user!.Identity!.Name);

            await cache.SetAsync($"AuthToken/{code}", token, TimeSpan.FromMinutes(5));

            return TypedResults.Redirect($"/RedeemToken/{code}");
        }

        return TypedResults.BadRequest();
    }

    internal static async Task<IResult> LogoutAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AuthApi");

        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
            await userService.SignOut();

        logger.LogInformation("Logged out user {UserName}", userPrincipal?.Identity?.Name);

        return TypedResults.Ok();
    }

    internal static IResult ValidateAsync(ClaimsPrincipal userPrincipal)
    {
        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
            return TypedResults.Ok();

        return TypedResults.Unauthorized();
    }

    internal static async Task<IResult> RefreshAsync(
        AuthToken token,
        [FromServices] AuthenticationService authenticationService)
    {
        try
        {
            var refreshed = await authenticationService.RefreshTokenAsync(token);

            return TypedResults.Ok(refreshed);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex);
        }
    }

    internal static async Task<IResult> RegisterAsync(
        [FromBody] RegisterModel model,
        [FromServices] AuthenticationService authenticationService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AuthApi");

        try
        {
            var token = await authenticationService.RegisterAsync(model.UserName, model.Password);

            return TypedResults.Ok(token);
        }
        catch (UserRegistrationException ex)
        {
            List<ErrorResponse.ErrorInfo> errorDetails =
                ex.IdentityResult?.Errors.Select(FromIdentityResult).ToList() ?? [];

            if (errorDetails.Count == 0)
            {
                errorDetails.Add(new ErrorResponse.ErrorInfo { Message = ex.Message });
            }

            var errorResponse = new ErrorResponse
            {
                Error = "UserRegistrationFailed",
                Message = "User registration failed.",
                Details = errorDetails,
            };

            return TypedResults.BadRequest(errorResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);

            return TypedResults.BadRequest(ex.Message);
        }
    }

    internal static IResult GetAuthenticationProvidersAsync(
        [FromServices] IMapper mapper,
        [FromServices] IOptions<Settings.Settings> settings)
    {
        var providers = mapper.Map<IEnumerable<AuthenticationProvider>>(
            settings.Value.Server.Authentication.AuthenticationProviders);

        return TypedResults.Ok(providers);
    }

    private static ErrorResponse.ErrorInfo FromIdentityResult(IdentityError error)
    {
        return new ErrorResponse.ErrorInfo
        {
            Key = error.Code,
            Message = error.Description,
        };
    }
}


