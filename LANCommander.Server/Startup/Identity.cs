using System.Text;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Identity
{
    public static WebApplicationBuilder AddIdentity(this WebApplicationBuilder builder, Settings settings)
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

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Administrator", policy =>
                policy
                    .RequireRole("Administrator"));
        
        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            if (settings.UseSSL)
            {
                options.Secure = settings.Authentication.HttpsCookiePolicy.Secure;
                options.MinimumSameSitePolicy = settings.Authentication.HttpsCookiePolicy.SameSite;
            }
            else
            {
                options.Secure = settings.Authentication.HttpCookiePolicy.Secure;
                options.MinimumSameSitePolicy = settings.Authentication.HttpCookiePolicy.SameSite;
            }
        });

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/AccessDenied";
            
            if (settings.UseSSL)
            {
                options.Cookie.SecurePolicy = settings.Authentication.HttpsCookiePolicy.Secure;
                options.Cookie.SameSite = settings.Authentication.HttpsCookiePolicy.SameSite;
            }
            else
            {
                options.Cookie.SecurePolicy = settings.Authentication.HttpCookiePolicy.Secure;
                options.Cookie.SameSite = settings.Authentication.HttpCookiePolicy.SameSite;
            }
        });

        return builder;
    }
}