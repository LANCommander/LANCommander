using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class GameSave : BaseModel
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
    }
}
