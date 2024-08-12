using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums
{
    public enum NetworkProtocol
    {
        [Display(Name = "TCP/IP")]
        TCPIP,
        IPX,
        Modem,
        Serial
    }
}
