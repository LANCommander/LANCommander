using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace LANCommander.Launcher.Startup;

public static class Aspire
{
    public static PhotinoBlazorAppBuilder AddAspire(this PhotinoBlazorAppBuilder builder)
    {
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }
}