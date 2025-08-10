using System.Net;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http.Features;

namespace LANCommander.Server.Startup;

public static class Kestrel
{
    public static WebApplicationBuilder ConfigureKestrel(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.Get<Settings>();

        if (settings?.UseSSL ?? false)
            builder.WebHost.UseUrls($"http://*:{settings.Port}", $"https://*:{settings.SSLPort}");
        else
            builder.WebHost.UseUrls($"http://*:{settings.Port}");
        
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = long.MaxValue;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            
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

        return builder;
    }
}