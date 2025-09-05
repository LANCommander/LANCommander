using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient(SDK.Client client) : IRpcClient
{
    private HubConnection _connection = default!;
    
    public IRpcHub Server { get; set; } = default!;
    
    public async Task ConnectAsync()
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(client.BaseUrl.Join("rpc"))
                .Build();

            Server = _connection.ServerProxy<IRpcHub>();

            _ = _connection.ClientRegistration<IRpcClient>(this);

            await _connection.StartAsync();
        }
        catch
        {
            
        }
    } 
}