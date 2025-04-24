using System.Net;

namespace LANCommander.SDK.Models;

public class BeaconResponseArgs
{
    public IPEndPoint EndPoint { get; set; }
    public BeaconMessage Message { get; set; }
}