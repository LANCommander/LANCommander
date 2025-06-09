using System.Management.Automation.Language;
using LANCommander.SDK;
using System.Timers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace LANCommander.Launcher.Services;

public class KeepAliveService : BaseService
{
    private readonly AuthenticationService AuthenticationService;

    private Timer CheckConnectionTimer;
    private Timer RetryConnectionTimer;
    
    private int PingInterval = 2000;
    private int RetryInterval = 1000;
    private int RetryCount;
    private int MaxRetries = 10;
    private bool ConnectionLost = false;

    private Models.ConnectionState ConnectionState = new();

    public event EventHandler ConnectionSevered;
    public event EventHandler ConnectionLostPermanently;
    public event EventHandler ConnectionEstablished;
    
    public KeepAliveService(
        Client client,
        ILogger<KeepAliveService> logger,
        AuthenticationService authenticationService) : base(client, logger)
    {
        AuthenticationService = authenticationService;

        AuthenticationService.OnLogin += (sender, args) => StartMonitoring();
        AuthenticationService.OnLogout += (sender, args) => StopMonitoring();
        AuthenticationService.OnRegister += (sender, args) => StartMonitoring();
        AuthenticationService.OnOfflineModeChanged += (state) =>
        {
            if (state)
                StopMonitoring();
            else
                StartMonitoring();
        };

        ConnectionEstablished += (sender, args) => StartMonitoring();
    }

    public Models.ConnectionState GetConnectionState()
    {
        return ConnectionState;
    }

    public void StartMonitoring()
    {
        RetryCount = 0;

        CheckConnectionTimer?.Stop();
        CheckConnectionTimer?.Dispose();
        
        CheckConnectionTimer = new Timer(PingInterval);
        CheckConnectionTimer.Elapsed += CheckConnection;
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
        var serverOnline = await AuthenticationService.IsServerOnlineAsync();

        if (!serverOnline && !ConnectionLost)
        {
            CheckConnectionTimer.Stop();
            CheckConnectionTimer.Dispose();
            CheckConnectionTimer = null;
            
            ConnectionLost = true;
            
            ConnectionSevered?.Invoke(this, EventArgs.Empty);
            
            RetryConnectionTimer = new Timer(RetryInterval);
            RetryConnectionTimer.Elapsed += RetryConnection;
            RetryConnectionTimer.AutoReset = true;
            RetryConnectionTimer.Start();
        }
    }

    private async void RetryConnection(object sender, ElapsedEventArgs e)
    {
        var serverOnline = await AuthenticationService.IsServerOnlineAsync();

        if (serverOnline && ConnectionLost)
        {
            ConnectionLost = false;
            
            RetryConnectionTimer.Stop();
            RetryConnectionTimer.Elapsed -= RetryConnection;
            
            ConnectionEstablished?.Invoke(this, EventArgs.Empty);
            
            AuthenticationService.SetOfflineMode(false);
        }
        else
        {
            RetryCount++;

            if (RetryCount == MaxRetries)
            {
                ConnectionLostPermanently?.Invoke(this, EventArgs.Empty);
                
                RetryConnectionTimer.Stop();
                RetryConnectionTimer.Elapsed -= RetryConnection;
                
                AuthenticationService.SetOfflineMode(true);
            }
        }
    }
}