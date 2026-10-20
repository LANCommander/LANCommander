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

    /// <summary>
    /// Set to <c>1</c> or <c>true</c> to stop the SDK from rejecting responses from a server
    /// whose major version differs from this client's.
    /// </summary>
    public const string SkipCompatibilityCheckEnvironmentVariable = "LANCOMMANDER_SKIP_VERSION_CHECK";

    // The executing assembly's version never changes during the process lifetime, but computing
    // it involves reflection (Assembly.GetExecutingAssembly().GetName()) which is surprisingly
    // costly when called repeatedly (e.g. once or twice per API request). Cache it once.
    private static readonly SemVersion _sdkVersion =
        SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0));

    private static readonly bool _enforceCompatibility = ResolveEnforceCompatibility();

    private static SemVersion? _currentVersion;

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

    /// <summary>
    /// Resolves the version to advertise to the server, preferring an explicit override, then the
    /// entry assembly's informational version, then this SDK assembly's version.
    /// </summary>
    /// <param name="overrideValue">An explicit version, e.g. from an environment variable. Ignored when null, blank or unparseable.</param>
    /// <param name="entryAssembly">The assembly to read <see cref="AssemblyInformationalVersionAttribute"/> from. May be null.</param>
    public static SemVersion Resolve(string? overrideValue, Assembly? entryAssembly)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue)
            && SemVersion.TryParse(overrideValue.Trim(), SemVersionStyles.Any, out var overridden))
            return overridden.WithoutMetadata();

        // The SDK appends the commit hash as source revision metadata (e.g. "2.1.11+abc1234") when no
        // explicit informational version is stamped. Semver treats that as build metadata, but strip it
        // anyway so the reported version stays readable in logs and headers.
        var informationalVersion = entryAssembly
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion)
            && SemVersion.TryParse(informationalVersion.Trim(), SemVersionStyles.Any, out var entryVersion))
            return entryVersion.WithoutMetadata();

        return _sdkVersion;
    }

    /// <summary>
    /// Whether an API version mismatch between this client and the server should be treated as a
    /// hard failure. False for local development builds and when
    /// <see cref="SkipCompatibilityCheckEnvironmentVariable"/> is set.
    /// </summary>
    public static bool EnforceCompatibility => _enforceCompatibility;

    private static bool ResolveEnforceCompatibility()
    {
        var value = Environment.GetEnvironmentVariable(SkipCompatibilityCheckEnvironmentVariable)?.Trim();

        if (!string.IsNullOrEmpty(value))
            return !(value == "1" || (bool.TryParse(value, out var skip) && skip));

#if DEBUG
        return false;
#else
        return true;
#endif
    }
}
