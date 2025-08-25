using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace LANCommander.Launcher.Startup;

public static class CommandLine
{
    public static bool ParseCommandLine(this PhotinoBlazorApp app, string[] args)
    {
        if (args.Length > 0)
        {
            using (var scope = app.Services.CreateScope())
            {
                var commandLineService = scope.ServiceProvider.GetRequiredService<CommandLineService>();
                
                Task.Run(async () => await commandLineService.ParseCommandLineAsync(args)).GetAwaiter().GetResult();
            }

            return true;
        }

        return false;
    }
}