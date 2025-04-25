using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class BeaconService
{
    public delegate void OnBeaconResponseHandler(object sender, BeaconResponseArgs e);

    public event OnBeaconResponseHandler OnBeaconResponse;
    
    private UdpClient _udpClient;
    private readonly Client _client;
    private readonly ILogger _logger;
    
    private List<DiscoveryProbe> _probeClients = new();
    private List<DiscoveryBeacon> _beaconClients = new();

    public BeaconService(Client client)
    {
        _client = client;
    }

    public BeaconService(Client client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Send broadcast packets across all interfaces to tell any server that we exist
    /// </summary>
    /// <param name="port">The port to beacon on</param>
    /// <param name="retryAttempts">The number of attempts to make before giving up</param>
    /// <param name="retryInterval">THe interval (in ms) between probe packets</param>
    /// <param name="cancellationToken"></param>
    public async Task StartProbeAsync(int port = 420, int retryAttempts = 10, int retryInterval = 2000,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        
        foreach (var networkInterface in GetNetworkInterfaces())
        {
            try
            {
                var probeClient = new DiscoveryProbe(networkInterface);

                await probeClient.BindSocketAsync(port);

                _probeClients.Add(probeClient);
            }
            catch
            {
                // ignored
            }
        }
        
        while (!cancellationToken.IsCancellationRequested)
        {
            if (attempt >= retryAttempts || cancellationToken.IsCancellationRequested)
                break;

            foreach (var probe in _probeClients)
            {
                await probe.SendAsync();
            }
            
            await Task.Delay(retryInterval, cancellationToken);
        }

        foreach (var client in _probeClients)
            client.Dispose();

        _probeClients.Clear();
    }

    /// <summary>
    /// Stop any active probes
    /// </summary>
    public async Task StopProbeAsync()
    {
        foreach (var probeClient in _probeClients)
        {
            probeClient.Dispose();
        }
    }

    public async Task StartBeaconAsync(
        int port,
        string address,
        string name)
    {
        foreach (var networkInterface in GetNetworkInterfaces())
        {
            try
            {
                var beaconClient = new DiscoveryBeacon(networkInterface);

                await beaconClient.StartAsync(port);

                beaconClient.OnProbe += async (beacon, probeEndPoint) =>
                {
                    var message = new BeaconMessage
                    {
                        Address = address,
                        Name = name,
                        Version = Client.GetCurrentVersion().ToString(),
                    };

                    await beacon.SendAsync(JsonSerializer.Serialize(message), probeEndPoint);
                };

                _beaconClients.Add(beaconClient);
            }
            catch (NetworkInformationException)
            {
                _logger?.LogError("Unable to start beacon on network interface {NetworkInterface}",
                    networkInterface.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unknown error while starting beacon on network interface {NetworkInterface}", networkInterface.Name);
            }
        }
    }

    public async Task StopBeaconAsync()
    {
        foreach (var beaconClient in _beaconClients)
        {
            beaconClient.Dispose();
        }
    }

    private IEnumerable<NetworkInterface> GetNetworkInterfaces()
    {
        var networkInterfaces = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                        i.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        return networkInterfaces;
    }

    private IEnumerable<IPAddress> GetBroadcastAddresses()
    {
        var networkInterfaces = GetNetworkInterfaces();
        
        foreach (var nic in networkInterfaces)
        {
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var ip = ua.Address;
                    var mask = ua.IPv4Mask;

                    if (mask == null)
                        continue;
                    
                    var ipBytes = ip.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();
                    var broadcastBytes = new byte[4];
                    
                    for (var i = 0; i < 4; i++)
                        broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));
                    
                    yield return new IPAddress(broadcastBytes);
                }
            }
        }
    }
}