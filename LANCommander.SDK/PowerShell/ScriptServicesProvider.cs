using System;
using System.Management.Automation;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell;

/// <summary>
/// Resolves LANCommander services that cmdlets need out of the runspace's session state.
/// PowerShell instantiates cmdlets itself via <see cref="Activator"/>, so cmdlets must have a
/// public parameterless constructor and cannot take constructor-injected dependencies. Services
/// are published into session state by <see cref="PowerShellScript"/> before the script runs.
/// </summary>
public static class ScriptServicesProvider
{
    /// <summary>
    /// Session state variable holding the <see cref="ProfileClient"/> for the current script.
    /// </summary>
    public const string ProfileClientKey = "LANCommander.SDK.ProfileClient";

    /// <summary>
    /// Session state variable holding the <see cref="ApiRequestFactory"/> for the current script.
    /// </summary>
    public const string ApiRequestFactoryKey = "LANCommander.SDK.ApiRequestFactory";

    /// <summary>
    /// Session state variable holding the <see cref="Abstractions.ISettingsProvider"/> for the current script.
    /// </summary>
    public const string SettingsProviderKey = "LANCommander.SDK.ISettingsProvider";

    /// <summary>
    /// Gets the profile client for the current script, or throws if the script is not running in a
    /// LANCommander context (for example, a bare runspace with no server connection).
    /// </summary>
    public static ProfileClient GetProfileClient(SessionState sessionState)
    {
        var profileClient = sessionState?.PSVariable.GetValue(ProfileClientKey) as ProfileClient;

        if (profileClient is null)
            throw new InvalidOperationException("ProfileClient not available in session state. This cmdlet must be run within a LANCommander script context.");

        return profileClient;
    }
}
