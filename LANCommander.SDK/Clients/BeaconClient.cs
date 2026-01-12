using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class BeaconClient(
    ILogger<BeaconClient> logger,
    INetworkInformationProvider networkInformationProvider)
{
    public delegate void OnBeaconResponseHandler(object sender, BeaconResponseArgs e);
    public event OnBeaconResponseHandler OnBeaconResponse;
    
    private List<DiscoveryProbe> _probeClients = new();
    private List<DiscoveryBeacon> _beaconClients = new();
    
    private List<IBeaconMessageInterceptor> _beaconMessageInterceptors = new();

    public void Initialize()
    {
        _beaconMessageInterceptors = new List<IBeaconMessageInterceptor>();
    }

    public BeaconClient AddBeaconMessageInterceptor(IBeaconMessageInterceptor interceptor)
    {
        _beaconMessageInterceptors.Add(interceptor);

        return this;
    }

    /// <summary>
    /// Send broadcast packets across all interfaces to tell any server that we exist
    /// </summary>
    /// <param name="port">The port to beacon on</param>
    /// <param name="retryAttempts">The number of attempts to make before giving up</param>
    /// <param name="retryInterval">THe interval (in ms) between probe packets</param>
    /// <param name="cancellationToken"></param>
    public async Task StartProbeAsync(int port = 35891, int retryAttempts = 10, int retryInterval = 2000,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        
        foreach (var networkInterface in networkInformationProvider.GetNetworkInterfaces())
        {
            DiscoveryProbe probeClient = null;
            try
            {
                probeClient = new DiscoveryProbe(networkInterface);

                await probeClient.BindSocketAsync(port);

                _probeClients.Add(probeClient);
            }
            catch
            {
                // ignored
                probeClient?.Dispose();
                _probeClients.Remove(probeClient);
            }
        }
        
        while (!cancellationToken.IsCancellationRequested)
        {
            if (attempt >= retryAttempts || cancellationToken.IsCancellationRequested)
                break;

            foreach (var probe in _probeClients)
            {
                if (probe.IsDisposed)
                    continue;
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

    /// <summary>
    /// Cleans up ressources created for probing
    /// </summary>
    /// <remarks>Clears list of current probe clients</remarks>
    public void CleanupProbe()
    {
        foreach (var probeClient in _probeClients)
        {
            if (!probeClient.IsDisposed)
            {
                probeClient.Dispose();
            }
        }

        _probeClients.Clear();
    }

    /// <summary>
    /// Start listening for probe broadcasts
    /// </summary>
    /// <param name="port">Port to listen on</param>
    /// <param name="address">The server address to send to the probe</param>
    /// <param name="name">The name of the server to send to the probe</param>
    public async Task StartBeaconAsync(
        int port,
        string address,
        string name)
    {
        foreach (var networkInterface in networkInformationProvider.GetNetworkInterfaces())
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
                        Version = VersionHelper.GetCurrentVersion().ToString(),
                    };

                    foreach (var interceptor in _beaconMessageInterceptors)
                    {
                        message = await interceptor.ExecuteAsync(message, beacon.InterfaceIPEndPoint);
                    }

                    await beacon.SendAsync(JsonSerializer.Serialize(message), probeEndPoint);
                };
                
                logger?.LogInformation("Started beacon on network interface {NetworkInterface}", networkInterface.Name);

                _beaconClients.Add(beaconClient);
            }
            catch (NetworkInformationException)
            {
                logger?.LogError("Unable to start beacon on network interface {NetworkInterface}",
                    networkInterface.Name);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unknown error while starting beacon on network interface {NetworkInterface}", networkInterface.Name);
            }
        }
    }

    /// <summary>
    /// Kill any running beacons
    /// </summary>
    public async Task StopBeaconAsync()
    {
        foreach (var beaconClient in _beaconClients)
        {
            beaconClient.Dispose();
        }
    }
}