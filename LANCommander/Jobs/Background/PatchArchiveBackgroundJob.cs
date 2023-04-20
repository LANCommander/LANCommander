using LANCommander.Services;

namespace LANCommander.Jobs.Background
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
            var originalArchive = await ArchiveService.Get(originalArchiveId);
            var alteredArchive = await ArchiveService.Get(alteredArchiveId);

            await ArchiveService.PatchArchive(originalArchive, alteredArchive);
        }
    }
}
