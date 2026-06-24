using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    public enum KeyAllocationMethod
    {
        [Display(Name = "MAC Address")]
        MacAddress,
        [Display(Name = "User Account")]
        UserAccount
    }
}
