using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class GameSave : BaseModel
    {
        public Guid GameId { get; set; }
        public virtual Game Game { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
    }
}
