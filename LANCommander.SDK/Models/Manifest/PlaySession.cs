using System;

namespace LANCommander.SDK.Models.Manifest
{
    public class PlaySession : BaseModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string User { get; set; }
    }
}
