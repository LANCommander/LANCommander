using System;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber(ITokenProvider tokenProvider, ILogger<RpcSubscriber> logger) : IRpcSubscriber
{
    private HubConnection _connection = default!;
    
    public async Task<bool> ConnectAsync(Uri serverAddress)
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(serverAddress.Join("rpc"), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(tokenProvider.GetToken().AccessToken);
                })
                .Build();

            RpcClient.Hub = _connection.ServerProxy<IRpcHub>();

            _ = _connection.ClientRegistration<IRpcSubscriber>(this);

            await _connection.StartAsync();

            return true;
        }
        catch(Exception ex) 
        {
            logger.LogError(ex, "Failed to connect to RPC server at {ServerAddress}", serverAddress);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            await _connection.StopAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disconnect from RPC server");
            return false;
        }
    }
}