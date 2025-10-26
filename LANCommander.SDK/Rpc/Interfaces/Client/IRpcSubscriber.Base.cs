using System;
using System.Threading.Tasks;
using LANCommander.SDK.Rpc.Server;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcSubscriber
{
    internal Task<bool> ConnectAsync(Uri serverAddress);
    internal Task<bool> DisconnectAsync();
}