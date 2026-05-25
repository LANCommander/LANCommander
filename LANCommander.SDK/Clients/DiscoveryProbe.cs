using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public class DiscoveryProbe : IDisposable
{
    private const int BufferSize = 1024;

    private bool _disposed = false;
    private int _port = 35891;
    private readonly UdpClient _udpClient;
    private readonly Socket _socket;
    private readonly IEnumerable<IPEndPoint> _broadcastEndpoints;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly byte[] _probeId;
    private readonly NetworkInterface _networkInterface;

    private byte[] _buffer = new byte[BufferSize];

    public delegate void OnBeaconResponseHandler(object sender, BeaconResponseArgs e);
    public event OnBeaconResponseHandler OnBeaconResponse;

    public DiscoveryProbe(NetworkInterface networkInterface)
    {
        _networkInterface = networkInterface;
        _udpClient = new UdpClient(0);
        _udpClient.EnableBroadcast = true;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        _broadcastEndpoints = networkInterface.GetBroadcastAddresses().Select(ba => new IPEndPoint(ba, _port));
        _probeId = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString());
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Send a probe packet to all network interface broadcast addresses
    /// </summary>
    public async Task SendAsync()
    {
        foreach (var endpoint in _broadcastEndpoints)
            _socket.SendTo(_probeId, endpoint);
    }

    /// <summary>
    /// Listen for responses from beacons
    /// </summary>
    /// <param name="port">Port to listen on/param>
    /// <exception cref="NetworkInformationException">Failed to bind to network interface</exception>
    public async Task BindSocketAsync(int port)
    {
        _port = port;

        var addressInformation = _networkInterface
            .GetIPProperties()
            .UnicastAddresses
            .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);

        if (addressInformation == null)
            throw new NetworkInformationException();

        EndPoint fromEndpoint = new IPEndPoint(IPAddress.Any, 0);

        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        _socket.Bind(new IPEndPoint(addressInformation.Address, _port));
        _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref fromEndpoint, ReceiveCallback, null);
    }

    /// <summary>
    /// Deserialize message from server as BeaconMessage
    /// </summary>
    /// <param name="ar"></param>
    private void ReceiveCallback(IAsyncResult ar)
    {
        EndPoint replyServer = new IPEndPoint(IPAddress.Any, 0);
        int receivedBytes = 0;

        try
        {
            receivedBytes = _socket.EndReceiveFrom(ar, ref replyServer);
        }
        catch (ObjectDisposedException)
        {
            // Socket closed intentionally - do not re-arm
            return;
        }
        catch (Exception)
        {
            // Socket error - still re-arm below
        }

        if (receivedBytes > 0)
        {
            try
            {
                var message = Encoding.UTF8.GetString(_buffer, 0, receivedBytes);

                OnBeaconResponse?.Invoke(this, new BeaconResponseArgs
                {
                    EndPoint = (IPEndPoint)replyServer,
                    Message = JsonSerializer.Deserialize<BeaconMessage>(message),
                });
            }
            catch (Exception)
            {
                // Non-JSON packet (e.g. broadcast loopback of own probe) - ignore, keep socket alive
            }
        }

        try
        {
            _buffer = new byte[BufferSize];
            _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref replyServer, ReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            // Socket was disposed between the receive and the re-arm
        }
    }

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        OnBeaconResponse = null;

        _udpClient?.Close();
        _udpClient?.Dispose();
        _socket?.Close();
        _socket?.Dispose();
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
