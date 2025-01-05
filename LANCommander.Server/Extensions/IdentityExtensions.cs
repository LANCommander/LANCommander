using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

namespace LANCommander.Server;

public static class IdentityExtensions
{
    public static void AddIdentity(this WebApplicationBuilder builder, LANCommanderSettings settings)
    {
        Log.Debug("Initializing Identity");
        builder.Services.AddDefaultIdentity<User>((IdentityOptions options) =>
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
        .AddDefaultTokenProviders();

        builder.Services.AddAuthentication(options =>
        {
            /*options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;*/
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
            });
    }
    }
