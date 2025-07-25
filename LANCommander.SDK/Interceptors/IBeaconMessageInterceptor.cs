using System.Net;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Interceptors;

public interface IBeaconMessageInterceptor
{
    Task<BeaconMessage> ExecuteAsync(BeaconMessage message, IPEndPoint interfaceEndPoint);
}