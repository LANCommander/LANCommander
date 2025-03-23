using System.Net;
using CoreRCON;

namespace LANCommander.Server.Services.Models;

public class RconConnection
{
    public RCON RCON { get; set; }
    public LogReceiver LogReceiver { get; set; }

    public RconConnection(string host, int port, string password)
    {
        RCON = new RCON(new IPEndPoint(IPAddress.Parse(host), port), password);
    }
}