using System.Net;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http.Features;

namespace LANCommander.Server.Startup;

public static class Kestrel
{
    public static WebApplicationBuilder ConfigureKestrel(this WebApplicationBuilder builder)
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

        return builder;
    }
}