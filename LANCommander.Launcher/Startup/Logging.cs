using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Serilog;

namespace LANCommander.Launcher.Startup;

public static class Logging
{
    public static PhotinoBlazorAppBuilder AddLogging(this PhotinoBlazorAppBuilder builder)
    {
        Log.Debug("Configuring logging...");

        /*builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
            loggingBuilder.AddSerilog(Logger);
        });*/

        return builder;
    }
}