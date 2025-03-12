using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Startup;

public static class Filesystem
{
    public static WebApplication PrepareDirectories(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<Settings>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        logger.LogDebug("Ensuring required directories exist");

        IEnumerable<string> directories = [
            settings.Update.StoragePath,
            settings.Launcher.StoragePath,
            settings.Backups.StoragePath,
            "Snippets",
        ];

        foreach (var directory in directories)
        {
            logger.LogDebug("Ensuring directory {Directory} exists", directory);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        return app;
    }
}