using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Startup;

public static class Authentication
{
    public static WebApplicationBuilder ConfigureAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }
    
    public static AuthenticationBuilder AddAuthenticationProviders(this AuthenticationBuilder authBuilder, Settings.Settings settings)
    {
        foreach (var authenticationProvider in settings.Server.Authentication.AuthenticationProviders)
        {
            try
            {
                switch (authenticationProvider.Type)
                {
                    case AuthenticationProviderType.OAuth2:
                        authBuilder.AddOAuth(authenticationProvider, settings);
                        break;

                    case AuthenticationProviderType.OpenIdConnect:
                        authBuilder.AddOpenIdConnect(authenticationProvider, settings);
                        break;

                    case AuthenticationProviderType.Saml:
                        throw new NotImplementedException("SAML providers are not supported at this time.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Authentication Provider {authenticationProvider.Name} could not be registered: {ex.Message}");
            }
        }

        return authBuilder;
    }
    
    public static AuthenticationBuilder AddOpenIdConnect(this AuthenticationBuilder authBuilder, AuthenticationProvider authenticationProvider, Settings.Settings settings)
    {
        if (String.IsNullOrWhiteSpace(authenticationProvider.Slug))
            return authBuilder;
        
        return authBuilder.AddOpenIdConnect(authenticationProvider.Slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.MetadataAddress = authenticationProvider.ConfigurationUrl;
            options.ResponseType = OpenIdConnectResponseType.Code;
            // Fetch the userinfo endpoint so configured claim mappings (which run over
            // the userinfo JSON) are applied to the principal.
            options.GetClaimsFromUserInfoEndpoint = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false
            };
            
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            if (options.MetadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                options.CorrelationCookie.SameSite = settings.Server.Authentication.HttpsCookiePolicy.SameSite;
                options.CorrelationCookie.SecurePolicy = settings.Server.Authentication.HttpsCookiePolicy.Secure;
                
                options.NonceCookie.SameSite = settings.Server.Authentication.HttpsCookiePolicy.SameSite;
                options.NonceCookie.SecurePolicy = settings.Server.Authentication.HttpsCookiePolicy.Secure;
                
                options.ResponseMode = OpenIdConnectResponseMode.Query;
            }
            else
            {
                options.CorrelationCookie.SameSite = settings.Server.Authentication.HttpCookiePolicy.SameSite;
                options.CorrelationCookie.SecurePolicy = settings.Server.Authentication.HttpCookiePolicy.Secure;
                
                options.NonceCookie.SameSite = settings.Server.Authentication.HttpCookiePolicy.SameSite;
                options.NonceCookie.SecurePolicy = settings.Server.Authentication.HttpCookiePolicy.Secure;
                
                options.ResponseMode = OpenIdConnectResponseMode.Query;
            }
                
            // Callbacks for middleware to properly correlate
            options.CallbackPath = new PathString($"/SignInOIDC");
            options.SignedOutCallbackPath = new PathString($"/SignOutOIDC");
            
            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }

            RegisterClaimActions(options.ClaimActions, authenticationProvider);

            options.Events.OnRemoteFailure = async context =>
            {
                Console.WriteLine($"Error: OIDC authentication failed: {context.Failure?.Message}");

                context.HandleResponse();
                context.Response.Redirect($"/Login?error={Uri.EscapeDataString(context.Failure?.Message ?? "Unknown authentication error")}");
            };

            options.Events.OnTicketReceived = async context =>
            {
                context.Properties.RedirectUri ??= context.ReturnUri;

                var identity = new ClaimsIdentity(context.Principal.Claims, IdentityConstants.ApplicationScheme);

                await ProcessLogin(context.HttpContext, context.Response, identity, authenticationProvider, context.Properties);

                context.HandleResponse();
            };
        });
    }

    public static AuthenticationBuilder AddOAuth(this AuthenticationBuilder authBuilder,
        AuthenticationProvider authenticationProvider, Settings.Settings settings)
    {
        if (String.IsNullOrWhiteSpace(authenticationProvider.Slug))
            return authBuilder;
        
        return authBuilder.AddOAuth(authenticationProvider.Slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.CallbackPath = new PathString($"/SignInOAuth");
            
            options.AuthorizationEndpoint = authenticationProvider.AuthorizationEndpoint;
            options.TokenEndpoint = authenticationProvider.TokenEndpoint;
            options.UserInformationEndpoint = authenticationProvider.UserInfoEndpoint;

            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }

            options.SaveTokens = true;

            RegisterClaimActions(options.ClaimActions, authenticationProvider);

            options.Events.OnRemoteFailure = async context =>
            {
                Console.WriteLine($"Error: OAuth authentication failed: {context.Failure?.Message}");

                context.HandleResponse();
                context.Response.Redirect($"/Login?error={Uri.EscapeDataString(context.Failure?.Message ?? "Unknown authentication error")}");
            };

            options.Events.OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException(
                        $"An error occurred while retrieving the user profile: {response.StatusCode}");

                var oauthUser = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                context.Identity.AddClaim(new Claim("Provider", authenticationProvider.Name));

                context.RunClaimActions(oauthUser.RootElement);
            };

            options.Events.OnTicketReceived = async context =>
            {
                context.Properties.RedirectUri ??= context.ReturnUri;

                var identity = new ClaimsIdentity(context.Principal.Claims, IdentityConstants.ApplicationScheme);

                await ProcessLogin(context.HttpContext, context.Response, identity, authenticationProvider, context.Properties);

                context.HandleResponse();
            };
        });
    }

    private static async Task ProcessLogin(
        HttpContext httpContext,
        HttpResponse response,
        ClaimsIdentity identity,
        AuthenticationProvider authenticationProvider,
        AuthenticationProperties properties)
    { 
        var signInManager = httpContext.RequestServices.GetService<SignInManager<User>>()!;
        var userService = httpContext.RequestServices.GetService<UserService>()!;
        var userCustomFieldService = httpContext.RequestServices.GetService<UserCustomFieldService>()!;
        
        var idClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

        User user;
        UserCustomField customField;
        
        var principal = new ClaimsPrincipal(identity);
        
        var action = properties.Items["Action"];

        switch (action)
        {
            case AuthenticationProviderActionType.Login:
                customField = await userCustomFieldService.FirstOrDefaultAsync(cf => cf.Name == authenticationProvider.GetCustomFieldName() && cf.Value == idClaim.Value);

                if (customField != null)
                {
                    user = await userService.GetAsync(customField.UserId.Value);

                    await SyncRolesAsync(httpContext, principal, user);

                    await signInManager.SignInAsync(user, true);

                    response.Redirect(properties.RedirectUri);
                }
                else
                {
                    user = await ProvisionUserAsync(httpContext, identity, authenticationProvider);

                    if (user != null)
                    {
                        await SyncRolesAsync(httpContext, principal, user);

                        await signInManager.SignInAsync(user, true);

                        response.Redirect(properties.RedirectUri);
                    }
                    else
                    {
                        // No usable username claim, or the username collides with an
                        // existing local account. Fall back to manual registration.
                        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme,
                            principal,
                            new AuthenticationProperties
                            {
                                AllowRefresh = false,
                                IsPersistent = false,
                            });

                        response.Redirect($"/Register?Provider={authenticationProvider.Slug}");
                    }
                }
                break;
            
            case AuthenticationProviderActionType.AccountLink:
                // Link accounts if needed
                var userId = Guid.Parse(properties.Items["UserId"]);
            
                user = await userService.GetAsync(userId);
            
                customField = await userCustomFieldService.FirstOrDefaultAsync(cf => cf.UserId == userId && cf.Name == authenticationProvider.GetCustomFieldName());
                
                var collidingCustomField = await userCustomFieldService.FirstOrDefaultAsync(cf => cf.Name == authenticationProvider.GetCustomFieldName() && cf.Value == idClaim.Value);
                
                if (collidingCustomField != null)
                    throw new Exception("This account is already linked to an existing user.");

                if (customField == null)
                {
                    await userCustomFieldService.AddAsync(new UserCustomField
                    {
                        Name = authenticationProvider.GetCustomFieldName(),
                        UserId = userId,
                        Value = idClaim.Value
                    });
                }
            
                await signInManager.SignInAsync(user, true);
                
                response.Redirect(properties.RedirectUri);
                break;
            
            case AuthenticationProviderActionType.Register:
                await httpContext.SignInAsync(IdentityConstants.ApplicationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        AllowRefresh = false,
                        IsPersistent = false,
                    });
                
                response.Redirect($"/Register?Provider={authenticationProvider.Slug}");
                break;
        }
    }

    private static void RegisterClaimActions(ClaimActionCollection claimActions, AuthenticationProvider authenticationProvider)
    {
        foreach (var claimMapping in authenticationProvider.ClaimMappings)
        {
            if (String.IsNullOrWhiteSpace(claimMapping.Name) || String.IsNullOrWhiteSpace(claimMapping.Value))
                continue;

            if (IsRoleMapping(claimMapping.Name))
                claimActions.Add(new RolesClaimAction(ProviderClaimTypes.Roles, claimMapping.Value));
            else
                claimActions.MapJsonKey(claimMapping.Name, claimMapping.Value);
        }
    }

    private static bool IsRoleMapping(string name)
    {
        return name.Equals(ProviderClaimTypes.Roles, StringComparison.OrdinalIgnoreCase)
            || name.Equals("roles", StringComparison.OrdinalIgnoreCase)
            || name.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindFirstAny(ClaimsIdentity identity, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claim = identity.FindFirst(claimType);

            if (claim != null && !String.IsNullOrWhiteSpace(claim.Value))
                return claim.Value;
        }

        return null;
    }

    private static async Task<User> ProvisionUserAsync(
        HttpContext httpContext,
        ClaimsIdentity identity,
        AuthenticationProvider authenticationProvider)
    {
        var userService = httpContext.RequestServices.GetService<UserService>()!;
        var roleService = httpContext.RequestServices.GetService<RoleService>()!;
        var userCustomFieldService = httpContext.RequestServices.GetService<UserCustomFieldService>()!;
        var settings = httpContext.RequestServices.GetService<SettingsProvider<Settings.Settings>>()!.CurrentValue;

        var idClaim = identity.FindFirst(ProviderClaimTypes.NameId);
        var username = FindFirstAny(identity, ProviderClaimTypes.Username, "username", "name", "preferred_username");

        if (idClaim == null || String.IsNullOrWhiteSpace(username))
            return null;

        // Never hijack an existing local account that happens to share this username.
        var existing = await userService.GetAsync(username);

        if (existing != null)
            return null;

        var user = new User
        {
            UserName = username,
            Email = FindFirstAny(identity, ProviderClaimTypes.Email, "email"),
            Alias = FindFirstAny(identity, ProviderClaimTypes.Alias, "alias"),
        };

        if (!settings.Server.Authentication.RequireApproval)
        {
            user.Approved = true;
            user.ApprovedOn = DateTime.UtcNow;
        }

        user = await userService.AddAsync(user);

        if (settings.Server.Roles.DefaultRoleId != Guid.Empty)
        {
            var defaultRole = await roleService.GetAsync(settings.Server.Roles.DefaultRoleId);

            if (defaultRole != null)
                await userService.AddToRoleAsync(user.UserName, defaultRole.Name);
        }

        await userCustomFieldService.AddAsync(new UserCustomField
        {
            UserId = user.Id,
            Name = authenticationProvider.GetCustomFieldName(),
            Value = idClaim.Value,
        });

        return user;
    }

    private static async Task SyncRolesAsync(HttpContext httpContext, ClaimsPrincipal principal, User user)
    {
        var userService = httpContext.RequestServices.GetService<UserService>()!;
        var roleService = httpContext.RequestServices.GetService<RoleService>()!;
        var settings = httpContext.RequestServices.GetService<SettingsProvider<Settings.Settings>>()!.CurrentValue;

        var claimedRoleNames = principal
            .FindAll(c => c.Type == ProviderClaimTypes.Roles || c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !String.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Roles that must never be removed automatically by a provider login.
        var protectedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RoleService.AdministratorRoleName
        };

        if (settings.Server.Roles.DefaultRoleId != Guid.Empty)
        {
            var defaultRole = await roleService.GetAsync(settings.Server.Roles.DefaultRoleId);

            if (defaultRole != null)
                protectedRoles.Add(defaultRole.Name);
        }

        // Create any roles named in the claims that don't yet exist.
        foreach (var roleName in claimedRoleNames)
        {
            if (await roleService.GetAsync(roleName) == null)
                await roleService.AddAsync(new Role { Name = roleName });
        }

        var currentRoleNames = (await userService.GetRolesAsync(user)).Select(r => r.Name).ToList();

        var rolesToAdd = claimedRoleNames
            .Where(rn => !currentRoleNames.Contains(rn, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var rolesToRemove = currentRoleNames
            .Where(rn => !claimedRoleNames.Contains(rn, StringComparer.OrdinalIgnoreCase))
            .Where(rn => !protectedRoles.Contains(rn))
            .ToList();

        if (rolesToAdd.Any())
            await userService.AddToRolesAsync(user.UserName, rolesToAdd);

        foreach (var roleName in rolesToRemove)
            await userService.RemoveFromRole(user.UserName, roleName);

        if (rolesToRemove.Any())
        {
            var cache = httpContext.RequestServices.GetService<IFusionCache>()!;

            await cache.RemoveByTagAsync(["User/Security", "User/Roles", $"User/{user.Id}", $"Library/{user.Id}"]);
        }
    }
}