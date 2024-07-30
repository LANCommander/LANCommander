using Semver;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class CheckForUpdateResponse
    {
        public bool UpdateAvailable { get; set; }
        public SemVersion Version { get; set; }
        public string DownloadUrl { get; set; }
    }
}
