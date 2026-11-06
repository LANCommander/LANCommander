using System;

namespace LANCommander.SDK.Models.Manifest
{
    public class Issue
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Game { get; set; }
        public bool IsPinned { get; set; }
    }
}
