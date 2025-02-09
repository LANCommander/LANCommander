using System.Net;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using Serilog;

namespace LANCommander.Server;

public static class AspNetExtensions
{
    public static void AddRazor(this WebApplicationBuilder builder, Settings settings)
    {
        Log.Debug("Configuring MVC and Blazor");
        builder.Services
            .AddMvc(static options => options.EnableEndpointRouting = false)
            .AddRazorOptions(static options =>
            {
                options.ViewLocationFormats.Clear();
                options.ViewLocationFormats.Add("/UI/Views/{1}/{0}.cshtml");
                options.ViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                options.ViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                options.AreaViewLocationFormats.Clear();
                options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{1}/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                options.PageViewLocationFormats.Clear();
                options.PageViewLocationFormats.Add("/UI/Pages/{1}/{0}.cshtml");
                options.PageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                options.PageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");

                options.AreaPageViewLocationFormats.Clear();
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/{1}/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
            });

        builder.Services.AddRazorPages(static options => options.RootDirectory = "/UI/Pages");

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
    }

    public static void AddSignalR(this WebApplicationBuilder builder)
    {
        builder.Services.AddSignalR().AddJsonProtocol(static options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });
    }

    public static void AddCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(static options => 
            options.AddPolicy("CorsPolicy", static builder =>
            {
                builder.AllowAnyHeader()
                       .AllowAnyMethod()
                       .SetIsOriginAllowed(static (host) => true)
                       .AllowCredentials();
            })
        );
    }

    public static void AddControllers(this WebApplicationBuilder builder)
    {
        Log.Debug("Initializing Controllers");
        builder.Services.AddControllers().AddJsonOptions(static x =>
        {
            x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    }

    public static void ConfigureKestrel(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            var settings = options.ApplicationServices.GetRequiredService<Settings>();
            
            options.Limits.MaxRequestBodySize = long.MaxValue;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            
            options.Listen(IPAddress.Any, settings.Port);
            
            if (settings.UseSSL)
            {
                options.Listen(IPAddress.Any, settings.SSLPort, listenOptions =>
                {
                    listenOptions.UseHttps(settings.CertificatePath, settings.CertificatePassword);
                });
            }
        });
        
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue;
        });
        
        builder.WebHost.UseStaticWebAssets();
    }

    public static void AddOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
    }
}