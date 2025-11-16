using IPXRelayDotNet;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class IPXRelayService : BaseService
    {
        private CancellationTokenSource? _relayCts;
        private Task? _relayTask;
        private IPXRelay? _relay;

        public IPXRelayService(ILogger<IPXRelayService> logger, SettingsProvider<Settings.Settings> settingsProvider) : base(logger, settingsProvider)
        {
            Init(logger);
        }

        public void Init(ILogger logger)
        {
            StopAsync().Wait();

            _relay = new IPXRelay(_settingsProvider.CurrentValue.Server.IPXRelay.Port, logger);

            if (!_settingsProvider.CurrentValue.Server.IPXRelay.Logging)
                _relay.DisableLogging();

            if (_settingsProvider.CurrentValue.Server.IPXRelay.Enabled)
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

            try
            {
                _relay?.Stop();
                _relay = null;
            }
            finally
            {
            }
        }
    }
}
