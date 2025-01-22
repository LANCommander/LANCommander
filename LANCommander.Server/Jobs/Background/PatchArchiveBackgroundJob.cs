using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class PatchArchiveBackgroundJob
    {
        private readonly ArchiveService ArchiveService;

        public PatchArchiveBackgroundJob(ArchiveService archiveService)
        {
            ArchiveService = archiveService;
        }

        public async Task Execute(Guid originalArchiveId, Guid alteredArchiveId)
        {
            var originalArchive = await ArchiveService.GetAsync(originalArchiveId);
            var alteredArchive = await ArchiveService.GetAsync(alteredArchiveId);

            await ArchiveService.PatchArchiveAsync(originalArchive, alteredArchive);
        }
    }
}
