using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services;

public class DiscoveryBeacon
{
    private const int BufferSize = 1024;
    private readonly NetworkInterface _networkInterface;
    private readonly UdpClient _udpClient;
    private readonly Socket _socket;

    private int _port = 35891;

    private byte[] _buffer = new byte[BufferSize];
    
    public IPEndPoint InterfaceIPEndPoint { get; private set; }

    public delegate void OnProbeHandler(DiscoveryBeacon beacon, IPEndPoint probeEndPoint);
    public event OnProbeHandler OnProbe;
    
    public DiscoveryBeacon(NetworkInterface networkInterface)
    {
        _networkInterface = networkInterface;
        _udpClient = new UdpClient(0);
        _udpClient.EnableBroadcast = true;
        
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    /// <summary>
    /// Listen for any broadcast packets on specified port
    /// </summary>
    /// <param name="port">Beacon port</param>
    /// <exception cref="NetworkInformationException">Failure to bind to a network interface</exception>
    public async Task StartAsync(int port)
    {
        _port = port;
        
        try
        {
            var addressInformation = _networkInterface
                .GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);

            if (addressInformation == null)
                throw new NetworkInformationException();

            EndPoint fromEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            InterfaceIPEndPoint = new IPEndPoint(addressInformation.Address, _port);

            _socket.Bind(InterfaceIPEndPoint);
            _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref fromEndPoint, ReceiveCallback,
                null);
        }
        catch (SocketException ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Send a message to an endpoint over UDP
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="endPoint">The endpoint of the client</param>
    public async Task SendAsync(string message, IPEndPoint endPoint)
    {
        var client = new UdpClient(_port);
        
        byte[] data = Encoding.UTF8.GetBytes(message);
        
        await client.SendAsync(data, data.Length, endPoint);
        client.Close();
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            EndPoint probe = new IPEndPoint(IPAddress.Any, 0);

            int receivedBytes = _socket.EndReceiveFrom(ar, ref probe);

            if (receivedBytes > 0)
            {
                byte[] response = new byte[receivedBytes];
                Array.Copy(_buffer, response, receivedBytes);

                OnProbe?.Invoke(this, (IPEndPoint)probe);
            }

            _buffer = new byte[BufferSize];
            _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref probe, ReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            // Socket closed
        }
        catch (Exception ex)
        {
            // Log error
        }
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        _socket?.Close();
        _socket?.Dispose();
    }
}