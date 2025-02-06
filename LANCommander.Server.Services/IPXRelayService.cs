using IPXRelayDotNet;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class IPXRelayService : BaseService
    {
        private IPXRelay Relay;

        public IPXRelayService(ILogger<IPXRelayService> logger) : base(logger)
        {
            if (Relay == null)
                Relay = new IPXRelay();

            Init();
        }

        public void Init()
        {
            if (Relay != null)
                Stop();

            if (Relay == null)
                Relay = new IPXRelay(_settings.IPXRelay.Port);

            if (!_settings.IPXRelay.Logging)
                Relay.DisableLogging();

            if (_settings.IPXRelay.Enabled)
                Relay.StartAsync();
        }

        public void Stop()
        {
            if (Relay != null)
                Relay.Dispose();

            Relay = null;
        }
    }
}
