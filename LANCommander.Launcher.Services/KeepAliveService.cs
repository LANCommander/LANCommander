using System.Timers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace LANCommander.Launcher.Services;

public class KeepAliveService : BaseService
{
    private readonly AuthenticationService _authenticationService;
    private readonly IConnectionClient _connectionClient;

    private Timer? _checkConnectionTimer;
    private Timer? _retryConnectionTimer;
    
    private readonly int _pingInterval = 2000;
    private readonly int _retryInterval = 1000;

    private int _retryCount;
    private readonly int _maxRetries = 30;
    private readonly int _retryGracePeriod = 10;

    private bool _connectionLost = false;
    private bool _isCheckConnectionActive = false;
    private bool _isRetryConnectionActive = false;

    public event EventHandler ConnectionSevered;
    public event EventHandler ConnectionRetryNext;
    public event EventHandler ConnectionLostPermanently;
    public event EventHandler ConnectionEstablished;
    
    public KeepAliveService(
        ILogger<KeepAliveService> logger,
        AuthenticationService authenticationService,
        IConnectionClient connectionClient) : base(logger)
    {
        _authenticationService = authenticationService;
        _connectionClient = connectionClient;
        
        connectionClient.OnConnect += (sender, args) => StartMonitoring();
        connectionClient.OnDisconnect += (sender, args) => StopMonitoring();

        ConnectionEstablished += (sender, args) => StartMonitoring();
    }

    public (int current, int total) GetRetryCount()
    {
        return (_retryCount, _maxRetries);
    }

    public void StartMonitoring()
    {
        _retryCount = 0;
        _connectionLost = false;

        _checkConnectionTimer?.Stop();
        _checkConnectionTimer?.Dispose();
        
        _checkConnectionTimer = new Timer(_pingInterval);
        _checkConnectionTimer.Elapsed += CheckConnection!;
        _checkConnectionTimer.AutoReset = true;
        _checkConnectionTimer.Start();
    }

    public void StopMonitoring()
    {
        _retryCount = 0;
        _checkConnectionTimer?.Stop();
        _checkConnectionTimer?.Dispose();
        _retryConnectionTimer?.Stop();
        _retryConnectionTimer?.Dispose();
    }

    private async void CheckConnection(object sender, ElapsedEventArgs e)
    {
        if (_isCheckConnectionActive) return;
        _isCheckConnectionActive = true;

        try
        {
            var serverOnline = await _authenticationService.IsServerOnlineAsync();

            if (!serverOnline && !_connectionLost)
            {
                _checkConnectionTimer?.Stop();
                _checkConnectionTimer?.Dispose();
                _checkConnectionTimer = null;

                _connectionLost = true;

                _retryConnectionTimer = new Timer(_retryInterval);
                _retryConnectionTimer.Elapsed += RetryConnection!;
                _retryConnectionTimer.AutoReset = false;
                _retryConnectionTimer.Start();
            }
        }
        finally
        {
            _isCheckConnectionActive = false;
        }
    }

    private async void RetryConnection(object sender, ElapsedEventArgs e)
    {
        if (_isRetryConnectionActive) return;
        _isRetryConnectionActive = true;

        try
        {
            if (_connectionClient.IsConnected() && _connectionLost)
            {
                _connectionLost = false;

                _retryConnectionTimer?.Stop();
                _retryConnectionTimer?.Dispose();
                _retryConnectionTimer = null;

                ConnectionEstablished?.Invoke(this, EventArgs.Empty);

                await _connectionClient.ConnectAsync();
            }
            else
            {
                _retryCount++;
                _retryConnectionTimer?.Start();
                ConnectionRetryNext?.Invoke(this, EventArgs.Empty);
                
                if (_retryCount == _retryGracePeriod)
                    ConnectionSevered?.Invoke(this, EventArgs.Empty);

                if (_retryCount == _maxRetries)
                {
                    _retryConnectionTimer?.Stop();
                    _retryConnectionTimer?.Dispose();
                    _retryConnectionTimer = null;

                    ConnectionLostPermanently?.Invoke(this, EventArgs.Empty);

                    await _connectionClient.EnableOfflineModeAsync();
                }
            }
        }
        finally
        {
            _isRetryConnectionActive = false;
        }
    }
}