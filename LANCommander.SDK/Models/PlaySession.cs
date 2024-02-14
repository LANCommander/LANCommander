using System;

namespace LANCommander.SDK.Models
{
    public class PlaySession : BaseModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public Guid GameId { get; set; }
    }
}
