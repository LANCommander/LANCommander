using LANCommander.SDK;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Startup;

public static class Filesystem
{
    public static WebApplication PrepareDirectories(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<Settings.Settings>>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        logger.LogDebug("Ensuring required directories exist");

        IEnumerable<string> directories = [
            settings.Value.Server.Update.StoragePath,
            settings.Value.Server.Launcher.StoragePath,
            settings.Value.Server.Backups.StoragePath,
            settings.Value.Server.Scripts.Snippets.StoragePath,
            settings.Value.Server.Scripts.Modules.StoragePath,
        ];

        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            var resolved = AppPaths.ResolveStorageLocationPath(directory);

            logger.LogDebug("Ensuring directory {Directory} exists", resolved);
            if (!Directory.Exists(resolved))
                Directory.CreateDirectory(resolved);
        }

        return app;
    }
}