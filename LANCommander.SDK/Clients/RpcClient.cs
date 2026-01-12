using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;

namespace LANCommander.SDK.Services;

// Base Client
public class RpcClient(IRpcSubscriber subscriber)
{
    internal static IRpcHub Hub { get; set; }
    
    IRpcSubscriber _subscriber = subscriber;

    public bool IsConnected => _subscriber.IsConnectedAsync().Result;

    public async Task ConnectAsync(Uri address)
        => await _subscriber.ConnectAsync(address);
    
    public async Task<bool> DisconnectAsync()
        => await _subscriber.DisconnectAsync();
}