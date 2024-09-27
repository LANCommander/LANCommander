using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    public class Lobby
    {
        public string Id { get; set; }
        public Guid GameId { get; set; }
        public string ExternalGameId { get; set; }
        public string ExternalUsername { get; set; }
        public string ExternalUserId { get; set; }
    }
}
