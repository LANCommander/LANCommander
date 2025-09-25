using System;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient(IServiceProvider serviceProvider) : IRpcClient
{
    private HubConnection _connection = default!;
    private readonly IConnectionClient _connectionClient = serviceProvider.GetService<IConnectionClient>();
    
    public IRpcHub Server { get; set; } = default!;
    
    public async Task<bool> ConnectAsync()
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_connectionClient.GetServerAddress().Join("rpc"))
                .Build();

            Server = _connection.ServerProxy<IRpcHub>();

            _ = _connection.ClientRegistration<IRpcClient>(this);

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