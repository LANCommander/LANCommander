using IPXRelayDotNet;

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
                Relay = new IPXRelay(Settings.IPXRelay.Port);

            if (!Settings.IPXRelay.Logging)
                Relay.DisableLogging();

            if (Settings.IPXRelay.Enabled)
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
