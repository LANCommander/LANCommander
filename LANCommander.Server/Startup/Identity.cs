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

        return builder;
    }
}