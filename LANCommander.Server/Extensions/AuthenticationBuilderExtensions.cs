using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LANCommander.Server.Extensions;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddOpenIdConnect(this AuthenticationBuilder authBuilder, AuthenticationProvider authenticationProvider)
    {
        var slug = authenticationProvider.GetSlug();
        
        return authBuilder.AddOpenIdConnect(slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.Authority = authenticationProvider.Authority;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false
            };

            options.Configuration = new OpenIdConnectConfiguration
            {
                AuthorizationEndpoint = authenticationProvider.AuthorizationEndpoint,
                TokenEndpoint = authenticationProvider.TokenEndpoint,
                UserInfoEndpoint = authenticationProvider.UserInfoEndpoint,
            };

            // Callbacks for middleware to properly correlate
            options.CallbackPath = new PathString($"/signin-oidc-{slug}");
            options.SignedOutCallbackPath = new PathString($"/signout-oidc-{slug}");
            
            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }
            
            foreach (var claimMapping in authenticationProvider.ClaimMappings)
            {
                if (!String.IsNullOrWhiteSpace(claimMapping.Name) && !String.IsNullOrWhiteSpace(claimMapping.Value))
                    options.ClaimActions.MapJsonKey(claimMapping.Name, claimMapping.Value);
            }
        });
    }

    public static AuthenticationBuilder AddOAuth(this AuthenticationBuilder authBuilder,
        AuthenticationProvider authenticationProvider)
    {
        var slug = authenticationProvider.GetSlug();
        
        return authBuilder.AddOAuth(slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.CallbackPath = new PathString($"/signin-oauth-{slug}");
            
            options.AuthorizationEndpoint = authenticationProvider.AuthorizationEndpoint;
            options.TokenEndpoint = authenticationProvider.TokenEndpoint;
            options.UserInformationEndpoint = authenticationProvider.UserInfoEndpoint;

            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }

            options.SaveTokens = true;

            foreach (var claimMapping in authenticationProvider.ClaimMappings)
            {
                if (!String.IsNullOrWhiteSpace(claimMapping.Name) && !String.IsNullOrWhiteSpace(claimMapping.Value))
                    options.ClaimActions.MapJsonKey(claimMapping.Name, claimMapping.Value);
            }

            options.Events.OnTicketReceived = async context =>
            {

            };

            // Retrieve user information
            /*options.Events.OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"An error occurred while retrieving the user profile: {response.StatusCode}");
                }

                var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                context.RunClaimActions(user.RootElement);
            };*/
        });
    }
}