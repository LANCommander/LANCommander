using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class KeyRequest
    {
        public Guid GameId { get; set; }
        public string MacAddress { get; set; }
        public string IpAddress { get; set; }
        public string ComputerName { get; set; }
    }
}
