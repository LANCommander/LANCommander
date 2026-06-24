using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Jobs.Background
{
    public class OptimizeImagesJob : BaseBackgroundJob
    {
        private readonly MediaService MediaService;
        private readonly ILogger<OptimizeImagesJob> Logger;

        public OptimizeImagesJob(
            ILogger<OptimizeImagesJob> logger,
            MediaService mediaService) : base(logger)
        {
            MediaService = mediaService;
            Logger = logger;
        }

        public override Task ExecuteAsync() => ExecuteAsync(new MediaOptimizationOptions());

        public async Task ExecuteAsync(MediaOptimizationOptions options)
        {
            var allMedia = await MediaService.Include(m => m.StorageLocation).GetAsync();

            long totalBefore = 0;
            long totalAfter = 0;
            var optimized = 0;

            foreach (var media in allMedia)
            {
                var result = await MediaService.OptimizeMediaAsync(media, options);

                if (!result.Changed)
                    continue;

                optimized++;
                totalBefore += result.BeforeBytes;
                totalAfter += result.AfterBytes;
            }

            Logger?.LogInformation(
                "Image optimization complete. Optimized {Count} media, reclaimed {Saved} bytes ({Before} -> {After})",
                optimized, totalBefore - totalAfter, totalBefore, totalAfter);
        }
    }
}
