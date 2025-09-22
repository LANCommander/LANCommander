using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services;

public interface IConnectionService
{
    public bool IsConnected();
    public Uri GetServerAddress();
    
    /// <summary>
    /// Set the server address to use for all API requests. Address is validated and checked for validity.
    /// </summary>
    /// <param name="address">The address to resolve for a LANCommander server</param>
    public Task UpdateServerAddressAsync(string address);

    public Task<bool> DisconnectAsync();
}