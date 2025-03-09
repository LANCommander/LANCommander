using IPXRelayDotNet;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class IPXRelayService : BaseService
    {
        private CancellationTokenSource? _relayCts;
        private Task? _relayTask;
        private IPXRelay? _relay;

        public IPXRelayService(ILogger<IPXRelayService> logger) : base(logger)
        {
            Init(logger);
        }

        public void Init(ILogger logger)
        {
            var settings = SettingService.GetSettings();

            StopAsync().Wait();

            _relay = new IPXRelay(settings.IPXRelay.Port, logger);

            if (!settings.IPXRelay.Logging)
                _relay.DisableLogging();

            if (settings.IPXRelay.Enabled)
            {
                _relayCts = new CancellationTokenSource();
                _relayTask = Task.Run(async () =>
                {
                    try
                    {
                        await _relay.StartAsync(_relayCts.Token);
                    }
                    catch (OperationCanceledException)
                    {

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start IPX Relay");
                    }
                }, _relayCts.Token);
            }
        }

        public async Task StopAsync()
        {
            if (_relayCts != null)
            {
                try
                {
                    _relayCts.CancelAsync();
                }
                catch (OperationCanceledException)
                {

                }
                finally
                {
                    _relayCts.Dispose();
                    _relayCts = null;
                }
            }

            _relay?.Stop();
            _relay?.Dispose();
            _relay = null;
        }
    }
}
