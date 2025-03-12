using Hangfire;
using LANCommander.Server.Jobs.Background;

namespace LANCommander.Server.Startup;

public static class Thumbnails
{
    public static WebApplication GenerateThumbnails(this WebApplication app)
    {
        BackgroundJob.Enqueue<GenerateThumbnailsJob>(x => x.ExecuteAsync());

        return app;
    }
}