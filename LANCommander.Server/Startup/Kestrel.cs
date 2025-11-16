using System.Net;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http.Features;

namespace LANCommander.Server.Startup;

public static class Kestrel
{
    public static WebApplicationBuilder ConfigureKestrel(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.Get<Settings.Settings>();

        if (settings?.Server.Http.UseSSL ?? false)
            builder.WebHost.UseUrls($"http://*:{settings.Server.Http.Port}", $"https://*:{settings.Server.Http.SSLPort}");
        else
            builder.WebHost.UseUrls($"http://*:{settings.Server.Http.Port}");
        
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = long.MaxValue;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            
            if (settings.Server.Http.UseSSL)
            {
                options.Listen(IPAddress.Any, settings.Server.Http.SSLPort, listenOptions =>
                {
                    listenOptions.UseHttps(settings.Server.Http.CertificatePath, settings.Server.Http.CertificatePassword);
                });
            }
        });
        
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue;
        });
        
        builder.WebHost.UseStaticWebAssets();

        return builder;
    }
}