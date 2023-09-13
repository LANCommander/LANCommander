using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("DHCPLeases")]
    public class DHCPLease : BaseModel
    {
        [Display(Name = "MAC Address")]
        public byte[] MACAddress { get; set; }

        [Display(Name = "IP Address")]
        public byte[] IPAddress { get; set; }
    }
}
