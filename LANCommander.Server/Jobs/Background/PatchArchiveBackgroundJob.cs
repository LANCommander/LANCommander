using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class PatchArchiveBackgroundJob
    {
        private readonly ArchiveService _archiveService;

        public PatchArchiveBackgroundJob(ArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        public async Task Execute(Guid originalArchiveId, Guid alteredArchiveId)
        {
            var originalArchive = await _archiveService.GetAsync(originalArchiveId);
            var alteredArchive = await _archiveService.GetAsync(alteredArchiveId);

            await _archiveService.PatchArchiveAsync(originalArchive, alteredArchive);
        }
    }
}
