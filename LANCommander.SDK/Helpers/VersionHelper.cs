using System;
using System.Reflection;
using Semver;

namespace LANCommander.SDK.Helpers;

public static class VersionHelper
{
    /// <summary>
    /// Set to <c>1</c> or <c>true</c> to stop the SDK from rejecting responses from a server
    /// whose major version differs from this client's.
    /// </summary>
    public const string SkipCompatibilityCheckEnvironmentVariable = "LANCOMMANDER_SKIP_VERSION_CHECK";

    // The executing assembly's version never changes during the process lifetime, but computing
    // it involves reflection (Assembly.GetExecutingAssembly().GetName()) which is surprisingly
    // costly when called repeatedly (e.g. once or twice per API request). Cache it once.
    private static readonly SemVersion _currentVersion =
        SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0));

    private static readonly bool _enforceCompatibility = ResolveEnforceCompatibility();

    public static SemVersion GetCurrentVersion() => _currentVersion;

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
