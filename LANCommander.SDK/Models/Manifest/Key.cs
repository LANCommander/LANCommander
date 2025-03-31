using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Key : BaseModel
    {
        public string Value { get; set; }
        public KeyAllocationMethod AllocationMethod { get; set; }
        public string ClaimedByMacAddress { get; set; }
        public string ClaimedByIpv4Address { get; set; }
        public string ClaimedByComputerName { get; set; }
        public string ClaimedByUser { get; set; }
        public DateTime? ClaimedOn { get; set; }
    }
}
