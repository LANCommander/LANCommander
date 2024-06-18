using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Enums
{
    public enum DownloadStatus
    {
        Idle,
        Downloading,
        InstallingRedistributables,
        InstallingMods,
        InstallingExpansions,
        RunningScripts,
        DownloadingSaves,
        Canceled,
        Failed,
        Complete
    }
}
