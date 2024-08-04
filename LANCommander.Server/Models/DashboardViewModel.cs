using ByteSizeLib;

namespace LANCommander.Server.Models
{
    public class DashboardViewModel
    {
        public ByteSize TotalAvailableFreeSpace { get; set; }
        public ByteSize TotalStorageSize { get; set; }
        public ByteSize TotalUploadDirectorySize { get; set; }
        public ByteSize TotalSaveDirectorySize { get; set; }
        public ByteSize TotalOtherSize {
            get
            {
                return TotalStorageSize - TotalAvailableFreeSpace - TotalUploadDirectorySize;
            }
        }

        public int GameCount { get; set; }
    }
}
