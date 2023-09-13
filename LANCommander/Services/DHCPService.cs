using DotNetProjects.DhcpServer;
using LANCommander.Data;
using LANCommander.Data.Models;
using NetworkPrimitives.Ipv4;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace LANCommander.Services
{
    public class DHCPService
    {
        private DHCPServer Server { get; set; }

        private Dictionary<string, IPAddress> Leases { get; set; } = new Dictionary<string, IPAddress>();

        private string NetworkInterfaceId { get; set; }
        private IPAddress SubnetMask { get; set; }
        private IPAddress Network { get; set; }
        private IPAddress RangeStart { get; set; }
        private IPAddress RangeEnd { get; set; }
        private string Domain { get; set; }
        private IPAddress ServerIdentifier { get; set; }
        private IPAddress DefaultGateway { get; set; }
        private IEnumerable<IPAddress> DnsServers { get; set; }

        public DHCPService()
        {
            Init();
        }

        public void Init()
        {
            var settings = SettingService.GetSettings();

            NetworkInterfaceId = settings.DHCPServer.NetworkInterface;
            SubnetMask = IPAddress.Parse(settings.DHCPServer.SubnetMask);
            Network = IPAddress.Parse(settings.DHCPServer.Network);
            RangeStart = IPAddress.Parse(settings.DHCPServer.RangeStart);
            RangeEnd = IPAddress.Parse(settings.DHCPServer.RangeEnd);
            Domain = settings.DHCPServer.Domain;
            ServerIdentifier = IPAddress.Parse(settings.DHCPServer.ServerIdentifier);
            DefaultGateway = IPAddress.Parse(settings.DHCPServer.DefaultGateway);
            DnsServers = settings.DHCPServer.DnsServers.Select(IPAddress.Parse);

            // Load stored leases
            // Leases = new ConcurrentBag<DHCPLease>(await Get());

            if (Server != null)
                Server.Dispose();

            if (settings.DHCPServer.Enabled)
                Start();
        }

        public void Start()
        {
            Server = new DHCPServer();

            Server.ServerName = "LANCommander";
            Server.OnDataReceived += OnRequest;
            Server.BroadcastAddress = IPAddress.Broadcast;

            Server.SendDhcpAnswerNetworkInterface = GetNetworkInterfaces().FirstOrDefault(nic => nic.Id == NetworkInterfaceId);
            Server.Start();
        }

        private void OnRequest(DHCPRequest request)
        {
            var type = request.GetMsgType();
            var mac = GetMacAddress(request.GetChaddr());

            IPAddress ip;

            if (!Leases.TryGetValue(mac, out ip))
            {
                ip = GetNextAddress();

                Leases[mac] = ip;
            }

            var reply = new DHCPReplyOptions
            {
                SubnetMask = SubnetMask,
                DomainName = Domain,
                ServerIdentifier = ServerIdentifier,
                RouterIP = DefaultGateway,
                DomainNameServers = DnsServers.ToArray()
            };

            switch (type)
            {
                case DHCPMsgType.DHCPDISCOVER:
                    request.SendDHCPReply(DHCPMsgType.DHCPOFFER, ip, reply);
                    break;

                case DHCPMsgType.DHCPREQUEST:
                    request.SendDHCPReply(DHCPMsgType.DHCPACK, ip, reply);
                    break;
            }
        }

        private IPAddress GetNextAddress()
        {
            var subnet = Ipv4Subnet.Parse($"{Network}/{GetBitmask(SubnetMask)}");
            
            foreach (var address in subnet.GetUsableAddresses())
            {
                var ip = address.ToIpAddress();

                if (ip.Address >= RangeStart.Address && ip.Address <= RangeEnd.Address && !Leases.Values.Any(l => l.Address == ip.Address))
                {
                    return ip;
                }
            }

            return null;
        }

        private ushort GetBitmask(IPAddress subnet)
        {
            var bytes = subnet.GetAddressBytes();

            ushort bitmask = 0;

            foreach (var b in bytes)
            {
                for (int i = 7; i >= 0; i--)
                {
                    if ((b & (1 << i)) != 0)
                        bitmask++;
                    else
                        break;
                }
            }

            return bitmask;
        }

        private string GetMacAddress(byte[] bytes)
        {
            return String.Join(':', bytes.Select(b => b.ToString("X2")));
        }

        public IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces();
        }

        public NetworkInterface GetActiveNetworkInterface()
        {
            var settings = SettingService.GetSettings();

            return GetNetworkInterfaces().FirstOrDefault(nic => nic.Id == settings.DHCPServer.NetworkInterface);
        }
    }
}
