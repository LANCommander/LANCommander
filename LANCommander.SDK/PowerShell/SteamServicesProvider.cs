using System;
using System.Management.Automation;
using System.Net.Http;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Options;
using LANCommander.Steam.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

/// <summary>
/// Provides Steam services for PowerShell cmdlets without dependency injection.
/// Services are created with <c>new</c> and cached in session state per runspace.
/// </summary>
public static class SteamServicesProvider
{
    private const string SteamCmdServiceKey = "LANCommander.Steam.SteamCmdService";
    private const string SteamWebApiServiceKey = "LANCommander.Steam.SteamStoreService";
    private const string SettingsProviderKey = "LANCommander.SDK.ISettingsProvider";
    private const string PSHostUIKey = "LANCommander.SDK.PSHostUI";

    /// <summary>
    /// Gets or creates the SteamCMD service for the current session.
    /// Uses host UI from session state (set by AsyncCmdlet) to log to the PowerShell runtime.
    /// </summary>
    public static ISteamCmdService GetSteamCmdService(SessionState sessionState)
    {
        var existing = sessionState.PSVariable.GetValue(SteamCmdServiceKey) as ISteamCmdService;
        
        if (existing != null)
            return existing;

        var settingsProvider = sessionState.PSVariable.GetValue(SettingsProviderKey) as LANCommander.SDK.Abstractions.ISettingsProvider;
        if (settingsProvider == null)
        {
            throw new InvalidOperationException("ISettingsProvider not found in session state. Ensure the PowerShell runspace is properly initialized.");
        }

        ILogger<SteamCmdService>? logger = null;
        var hostUI = sessionState.PSVariable.GetValue(PSHostUIKey) as System.Management.Automation.Host.PSHostUserInterface;
        if (hostUI != null)
        {
            logger = new PowerShellHostLogger<SteamCmdService>(hostUI);
        }

        var options = new SteamCmdOptions
        {
            ExecutablePath = settingsProvider.CurrentValue.Steam.Path,
            AutoDetectPath = true
        };
        
        var profileStore = new Providers.SteamCmdProfileStore(settingsProvider);
        var service = new SteamCmdService(options, profileStore, logger);
        
        sessionState.PSVariable.Set(SteamCmdServiceKey, service);
        
        return service;
    }

    /// <summary>
    /// Gets or creates the Steam Store service for the current session.
    /// </summary>
    public static ISteamWebApiService GetSteamWebApiService(SessionState sessionState)
    {
        var existing = sessionState.PSVariable.GetValue(SteamWebApiServiceKey) as SteamWebApiService;
        
        if (existing != null)
            return existing;

        var service = new SteamWebApiService(new HttpClient());
        
        sessionState.PSVariable.Set(SteamWebApiServiceKey, service);
        
        return service;
    }
    
    
}
