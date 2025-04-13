using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class BeaconService(
        ILogger<BeaconService> logger,
        IVersionProvider versionProvider) : BaseService(logger), IHostedService
    {
        private UdpClient _udpClient;
        
        private List<BeaconSocket> _sockets = new();
        private string _replyMessage;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            string[] dataParts =
            [
                _settings.Beacon?.Address,
                _settings.Beacon?.Name,
                versionProvider.GetCurrentVersion().ToString()
            ];

            _replyMessage = String.Join('|', dataParts);

            foreach (var networkInterface in networkInterfaces.Where(nic => nic.OperationalStatus == OperationalStatus.Up))
            {
                await BindSocketAsync(networkInterface);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var socket in _sockets)
            {
                socket.Close();
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets up a UDP client to respond back to the requester
        /// </summary>
        /// <param name="message">The message to send back</param>
        /// <param name="endpoint">The endpoint of the receiving client</param>
        private async Task SendAsync(string message, IPEndPoint endpoint)
        {
            var client = new UdpClient(0);
            
            byte[] data = Encoding.ASCII.GetBytes(message);
            
            await client.SendAsync(data, data.Length, endpoint);
            client.Close();
        }

        private Task BindSocketAsync(NetworkInterface networkInterface)
        {
            try
            {
                var socket = new BeaconSocket(networkInterface, _settings.Port);
                
                socket.BeginReceive(async void (ar) =>
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    
                    byte[] data = _udpClient.EndReceive(ar, ref endpoint);
                    
                    // We could do something with this message if we wanted to encode more data
                    var message = Encoding.ASCII.GetString(data);
                    
                    await SendAsync(_replyMessage, endpoint);
                });
                
                _sockets.Add(socket);
            }
            catch (Exception ex)
            {
                logger?.LogError("Could not bind socket for beacon on interface {Interface}", networkInterface.Name);
                logger?.LogError(ex, ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
