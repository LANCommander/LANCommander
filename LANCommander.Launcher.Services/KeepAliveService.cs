using System.Management.Automation.Language;
using LANCommander.SDK;
using System.Timers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace LANCommander.Launcher.Services;

public class KeepAliveService : BaseService
{
    private readonly AuthenticationService AuthenticationService;

    private Timer? CheckConnectionTimer;
    private Timer? RetryConnectionTimer;
    
    private int PingInterval = 2000;
    private int RetryInterval = 1000;

    private int RetryCount;
    private int MaxRetries = 10;

    private bool ConnectionLost = false;
    private bool IsCheckConnectionActive = false;
    private bool IsRetryConnectionActive = false;

    public event EventHandler ConnectionSevered;
    public event EventHandler ConnectionRetryNext;
    public event EventHandler ConnectionLostPermanently;
    public event EventHandler ConnectionEstablished;
    
    public KeepAliveService(
        ILogger<KeepAliveService> logger,
        AuthenticationService authenticationService,
        IConnectionClient connectionClient) : base(logger)
    {
        AuthenticationService = authenticationService;
        
        connectionClient.OnConnect += (sender, args) => StartMonitoring();
        connectionClient.OnDisconnect += (sender, args) => StopMonitoring();

        ConnectionEstablished += (sender, args) => StartMonitoring();
    }

    public (int current, int total) GetRetryCount()
    {
        return (RetryCount, MaxRetries);
    }

    public void StartMonitoring()
    {
        RetryCount = 0;
        ConnectionLost = false;

        CheckConnectionTimer?.Stop();
        CheckConnectionTimer?.Dispose();
        
        CheckConnectionTimer = new Timer(PingInterval);
        CheckConnectionTimer.Elapsed += CheckConnection!;
        CheckConnectionTimer.AutoReset = true;
        CheckConnectionTimer.Start();
    }

    public void StopMonitoring()
    {
        RetryCount = 0;
        CheckConnectionTimer?.Stop();
        CheckConnectionTimer?.Dispose();
        RetryConnectionTimer?.Stop();
        RetryConnectionTimer?.Dispose();
    }

    private async void CheckConnection(object sender, ElapsedEventArgs e)
    {
        if (IsCheckConnectionActive) return;
        IsCheckConnectionActive = true;

        try
        {
            var serverOnline = await AuthenticationService.IsServerOnlineAsync();

            if (!serverOnline && !ConnectionLost)
            {
                CheckConnectionTimer?.Stop();
                CheckConnectionTimer?.Dispose();
                CheckConnectionTimer = null;

                ConnectionLost = true;

                ConnectionSevered?.Invoke(this, EventArgs.Empty);

                RetryConnectionTimer = new Timer(RetryInterval);
                RetryConnectionTimer.Elapsed += RetryConnection!;
                RetryConnectionTimer.AutoReset = false;
                RetryConnectionTimer.Start();
            }
        }
        finally
        {
            IsCheckConnectionActive = false;
        }
    }

    private async void RetryConnection(object sender, ElapsedEventArgs e)
    {
        if (IsRetryConnectionActive) return;
        IsRetryConnectionActive = true;

        try
        {
            var serverOnline = await AuthenticationService.IsServerOnlineAsync();

            if (serverOnline && ConnectionLost)
            {
                ConnectionLost = false;

                RetryConnectionTimer?.Stop();
                RetryConnectionTimer?.Dispose();
                RetryConnectionTimer = null;

                ConnectionEstablished?.Invoke(this, EventArgs.Empty);

                await AuthenticationService.SetOfflineModeAsync(false);
            }
            else
            {
                RetryCount++;
                RetryConnectionTimer?.Start();
                ConnectionRetryNext?.Invoke(this, EventArgs.Empty);

                if (RetryCount == MaxRetries)
                {
                    RetryConnectionTimer?.Stop();
                    RetryConnectionTimer?.Dispose();
                    RetryConnectionTimer = null;

                    ConnectionLostPermanently?.Invoke(this, EventArgs.Empty);

                    await AuthenticationService.SetOfflineModeAsync(true);
                }
            }
        }
        finally
        {
            IsRetryConnectionActive = false;
        }
    }
}