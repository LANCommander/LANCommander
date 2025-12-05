using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.ImportExport.Legacy.Enums;

internal enum NetworkProtocol
{
    [Display(Name = "TCP/IP")]
    TCPIP,
    IPX,
    Modem,
    Serial,
    Lobby
}