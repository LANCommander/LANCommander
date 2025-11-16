using System.Text;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Identity
{
    public static WebApplicationBuilder AddIdentity(this WebApplicationBuilder builder)
    {
        var settings = new Settings.Settings();
        builder.Configuration.Bind(settings);
        
        Log.Debug("Initializing Identity");
        builder.Services.AddIdentityCore<User>((options) =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequireNonAlphanumeric = settings.Server.Authentication.PasswordRequireNonAlphanumeric;
            options.Password.RequireLowercase = settings.Server.Authentication.PasswordRequireLowercase;
            options.Password.RequireUppercase = settings.Server.Authentication.PasswordRequireUppercase;
            options.Password.RequireDigit = settings.Server.Authentication.PasswordRequireDigit;
            options.Password.RequiredLength = settings.Server.Authentication.PasswordRequiredLength;
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Server.Authentication.TokenSecret))
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
            if (settings.Server.Http.UseSSL)
            {
                options.Secure = settings.Server.Authentication.HttpsCookiePolicy.Secure;
                options.MinimumSameSitePolicy = settings.Server.Authentication.HttpsCookiePolicy.SameSite;
            }
            else
            {
                options.Secure = settings.Server.Authentication.HttpCookiePolicy.Secure;
                options.MinimumSameSitePolicy = settings.Server.Authentication.HttpCookiePolicy.SameSite;
            }
        });

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/AccessDenied";
            
            if (settings.Server.Http.UseSSL)
            {
                options.Cookie.SecurePolicy = settings.Server.Authentication.HttpsCookiePolicy.Secure;
                options.Cookie.SameSite = settings.Server.Authentication.HttpsCookiePolicy.SameSite;
            }
            else
            {
                options.Cookie.SecurePolicy = settings.Server.Authentication.HttpCookiePolicy.Secure;
                options.Cookie.SameSite = settings.Server.Authentication.HttpCookiePolicy.SameSite;
            }
        });

        return builder;
    }
}