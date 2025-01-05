using Serilog;

namespace LANCommander.Server;

public static class AspNetExtensions
{
    public static void AddRazor(this WebApplicationBuilder builder)
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
            .AddServerSideBlazor()
            .AddCircuitOptions(static option => option.DetailedErrors = true)
            .AddHubOptions(static option =>
            {
                option.MaximumReceiveMessageSize = 1024 * 1024 * 11;
                option.DisableImplicitFromServicesParameters = true;
            });
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
}