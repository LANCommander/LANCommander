using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class PatchArchiveBackgroundJob
    {
        private readonly ArchivePatchService _archivePatchService;

        public PatchArchiveBackgroundJob(ArchivePatchService archivePatchService)
        {
            _archivePatchService = archivePatchService;
        }

        public async Task Execute(Guid originalArchiveId, Guid alteredArchiveId)
        {
            await _archivePatchService.GeneratePatchAsync(originalArchiveId, alteredArchiveId);
        }
    }
}
