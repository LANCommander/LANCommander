using System;

namespace LANCommander.SDK.Models
{
    public class GameExternalId : BaseModel
    {
        public string Provider { get; set; }
        public string ExternalId { get; set; }
    }
}
