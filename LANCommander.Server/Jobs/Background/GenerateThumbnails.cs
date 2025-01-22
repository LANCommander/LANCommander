using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class GenerateThumbnailsJob : BaseBackgroundJob
    {
        private readonly MediaService MediaService;

        public GenerateThumbnailsJob(
            ILogger<GenerateThumbnailsJob> logger,
            MediaService mediaService) : base(logger)
        {
            MediaService = mediaService;
        }

        public override async Task ExecuteAsync()
        {
            var allMedia = await MediaService.GetAsync();

            foreach (var media in allMedia)
            {
                if (!MediaService.ThumbnailExists(media))
                    await MediaService.GenerateThumbnailAsync(media);
            }
        }
    }
}
