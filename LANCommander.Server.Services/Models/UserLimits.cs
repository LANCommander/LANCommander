namespace LANCommander.Server.Services.Models
{
    public readonly record struct UserLimits(int StorageQuotaMB, int DownloadSpeedKBps, bool SavesEnabled)
    {
        public long StorageQuotaBytes => StorageQuotaMB <= 0 ? 0 : (long)StorageQuotaMB * 1024 * 1024;
        public long DownloadSpeedBytesPerSecond => DownloadSpeedKBps <= 0 ? 0 : (long)DownloadSpeedKBps * 1024;
        public bool StorageUnlimited => StorageQuotaMB <= 0;
        public bool DownloadUnlimited => DownloadSpeedKBps <= 0;
    }
}
