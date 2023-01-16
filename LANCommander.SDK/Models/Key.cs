using System;

namespace LANCommander.SDK.Models
{
    public class Key : BaseModel
    {
        public string Value { get; set; }
        public virtual Game Game { get; set; }
        public KeyAllocationMethod AllocationMethod { get; set; }
        public string ClaimedByMacAddress { get; set; }
        public string ClaimedByIpv4Address { get; set; }
        public string ClaimedByComputerName { get; set; }
        public DateTime? ClaimedOn { get; set; }
    }

    public enum KeyAllocationMethod
    {
        UserAccount,
        MacAddress
    }
}
