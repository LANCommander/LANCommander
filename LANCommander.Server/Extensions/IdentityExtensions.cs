using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;

namespace LANCommander.Server;

public static class IdentityExtensions
{
    public static void AddIdentity(this WebApplicationBuilder builder, Settings settings)
    {
        Log.Debug("Initializing Identity");
        builder.Services.AddIdentityCore<User>((options) =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequireNonAlphanumeric = settings.Authentication.PasswordRequireNonAlphanumeric;
            options.Password.RequireLowercase = settings.Authentication.PasswordRequireLowercase;
            options.Password.RequireUppercase = settings.Authentication.PasswordRequireUppercase;
            options.Password.RequireDigit = settings.Authentication.PasswordRequireDigit;
            options.Password.RequiredLength = settings.Authentication.PasswordRequiredLength;
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<Data.DatabaseContext>()
        .AddUserManager<UserManager<User>>()
        .AddSignInManager<SignInManager<User>>()
        .AddRoleManager<RoleManager<Role>>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    // ValidAudience = configuration["JWT:ValidAudience"],
                    // ValidIssuer = configuration["JWT:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Authentication.TokenSecret))
                };
            })
            .AddAuthenticationProviders(settings)
            .AddIdentityCookies();
        
        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.Secure = settings.Authentication.CookieSecurePolicy;
            options.MinimumSameSitePolicy = settings.Authentication.MinimumSameSitePolicy;
        });

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/AccessDenied";
        });
    }

    public static AuthenticationBuilder AddAuthenticationProviders(this AuthenticationBuilder authBuilder, Settings settings)
    {
        foreach (var authenticationProvider in settings.Authentication.AuthenticationProviders)
        {
            try
            {
                switch (authenticationProvider.Type)
                {
                    case AuthenticationProviderType.OAuth2:
                        authBuilder.AddOAuth(authenticationProvider);
                        break;

                    case AuthenticationProviderType.OpenIdConnect:
                        authBuilder.AddOpenIdConnect(authenticationProvider);
                        break;

                    case AuthenticationProviderType.Saml:
                        throw new NotImplementedException("SAML providers are not supported at this time.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Authentication Provider {Name} could not be registered",
                    authenticationProvider.Name);
            }
        }

        return authBuilder;
    }
}
