using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public class PatchArchiveBackgroundJob
    {
        private readonly ArchiveClient _archiveClient;

        public PatchArchiveBackgroundJob(ArchiveClient archiveClient)
        {
            _archiveClient = archiveClient;
        }

        public async Task Execute(Guid originalArchiveId, Guid alteredArchiveId)
        {
            var originalArchive = await _archiveClient.GetAsync(originalArchiveId);
            var alteredArchive = await _archiveClient.GetAsync(alteredArchiveId);

            await _archiveClient.PatchArchiveAsync(originalArchive, alteredArchive);
        }
    }
}
