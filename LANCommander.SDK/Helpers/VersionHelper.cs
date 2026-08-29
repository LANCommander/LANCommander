using System;
using System.Reflection;
using Semver;

namespace LANCommander.SDK.Helpers;

public static class VersionHelper
{
    /// <summary>
    /// Environment variable that overrides the version this client reports. Intended for local
    /// debugging against a server on a different release, where a source build would otherwise
    /// report a version that trips the major-version compatibility gate.
    /// </summary>
    public const string VersionEnvironmentVariable = "LANCOMMANDER_VERSION";

    private static SemVersion _currentVersion;

    /// <summary>
    /// Gets the version this client identifies as. It is sent to the server on every request via the
    /// <c>X-API-Version</c> header and is the value compared against the server's own version to
    /// decide API compatibility.
    /// </summary>
    /// <remarks>
    /// Resolution order: the <see cref="VersionEnvironmentVariable"/> override, then the entry
    /// assembly's informational version, then the SDK assembly's version.
    /// </remarks>
    public static SemVersion GetCurrentVersion()
    {
        return _currentVersion ??= Resolve(
            Environment.GetEnvironmentVariable(VersionEnvironmentVariable),
            Assembly.GetEntryAssembly());
    }

    internal static SemVersion Resolve(string overrideValue, Assembly entryAssembly)
    {
        if (SemVersion.TryParse(overrideValue, SemVersionStyles.Any, out var overrideVersion))
            return overrideVersion;

        var informationalVersion = entryAssembly
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        // The SDK appends the commit hash as source revision metadata (e.g. "2.1.11+abc1234") when no
        // explicit informational version is stamped. Semver treats that as build metadata, but strip it
        // anyway so the reported version stays readable in logs and headers.
        var separatorIndex = informationalVersion?.IndexOf('+') ?? -1;

        if (separatorIndex > 0)
            informationalVersion = informationalVersion.Substring(0, separatorIndex);

        if (SemVersion.TryParse(informationalVersion, SemVersionStyles.Any, out var assemblyVersion))
            return assemblyVersion;

        return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
    }
}
