using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class RepackArchiveBackgroundJob
    {
        private readonly ArchiveService _archiveService;

        public RepackArchiveBackgroundJob(ArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        public async Task Execute(Guid archiveId)
        {
            await _archiveService.RepackArchiveAsync(archiveId);
        }
    }
}
