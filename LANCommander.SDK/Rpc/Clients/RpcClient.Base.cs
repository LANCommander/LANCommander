using System;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber(ITokenProvider tokenProvider) : IRpcSubscriber
{
    private HubConnection _connection = default!;
    
    public async Task<bool> ConnectAsync(Uri serverAddress)
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(serverAddress.Join("rpc"), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(tokenProvider.GetToken());
                })
                .Build();

            RpcClient.Hub = _connection.ServerProxy<IRpcHub>();

            _ = _connection.ClientRegistration<IRpcSubscriber>(this);

            await _connection.StartAsync();

            return true;
        }
        catch
        {
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
        catch
        {
            return false;
        }
    }
}