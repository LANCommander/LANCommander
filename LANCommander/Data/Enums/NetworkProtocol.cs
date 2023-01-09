using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Enums
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
