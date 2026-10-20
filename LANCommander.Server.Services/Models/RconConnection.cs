using System.Net;
using System.Net.Sockets;
using CoreRCON;

namespace LANCommander.Server.Services.Models;

public class RconConnection : IDisposable
{
    public RCON RCON { get; set; }
    public LogReceiver LogReceiver { get; set; }

    public string Host { get; }
    public int Port { get; }

    public bool IsConnected { get; private set; }

    private readonly SemaphoreSlim _gate = new(1, 1);

    public RconConnection(string host, int port, string password)
    {
        Host = host;
        Port = port;
        RCON = new RCON(new IPEndPoint(ResolveAddress(host), port), password);
        RCON.OnDisconnected += () => IsConnected = false;
    }

    /// <summary>
    /// Sends a command, connecting (and authenticating) first if needed. Commands are serialized
    /// per connection because RCON is a request/response protocol over one socket.
    /// </summary>
    /// <returns>The server's response text.</returns>
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!IsConnected)
            {
                await RCON.ConnectAsync().WaitAsync(cancellationToken);
                IsConnected = true;
            }

            return await RCON.SendCommandAsync(command).WaitAsync(cancellationToken);
        }
        catch
        {
            IsConnected = false;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
            return address;

        var addresses = Dns.GetHostAddresses(host);

        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new ArgumentException($"Could not resolve RCON host \"{host}\"", nameof(host));
    }

    public void Dispose()
    {
        IsConnected = false;
        RCON?.Dispose();
        _gate.Dispose();
    }
}
