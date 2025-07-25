using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LANCommander.Server.Services.Models;

public class BeaconSocket
{
    private const int BufferSize = 1024;
    
    private Socket _socket;
    private byte[] _buffer = new byte[BufferSize];
    private EndPoint _fromEndPoint = new IPEndPoint(IPAddress.Any, 0);
    private AsyncCallback _callback;

    public BeaconSocket(NetworkInterface networkInterface, int port)
    {
        var addressInformation = networkInterface
            .GetIPProperties()
            .UnicastAddresses
            .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);

        if (addressInformation == null)
            throw new NetworkInformationException();
        
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(addressInformation.Address, port));
    }

    public void BeginReceive(AsyncCallback callback)
    {
        _callback = callback;
        _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref _fromEndPoint, _callback, null);
    }

    public void Close()
    {
        _socket.Close();
    }
}