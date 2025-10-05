using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services;

public interface IConnectionClient
{
    public event EventHandler OnConnect;
    public event EventHandler OnDisconnect;
    public event EventHandler OnServerAddressChanged;
    public event EventHandler OnOfflineModeEnabled;
    
    public bool IsConnected();
    public bool IsConfigured();
    public bool IsOfflineMode();
    public bool HasServerAddress();
    public Uri GetServerAddress();
    
    /// <summary>
    /// Set the server address to use for all API requests. Address is validated and checked for validity.
    /// </summary>
    /// <param name="address">The address to resolve for a LANCommander server</param>
    public Task UpdateServerAddressAsync(string address);
    public Task UpdateServerAddressAsync(Uri address);

    public Task<bool> ConnectAsync();
    public Task<bool> DisconnectAsync();
    public Task EnableOfflineModeAsync();
    public Task<bool> PingAsync(Uri serverAddress = null);
}