using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LANCommander.SDK.Tests.PowerShell;

/// <summary>
/// Elevation state of the test host. Writing to HKLM is a privileged operation, so the tests that
/// prove an elevated script can do it are only meaningful when the test process itself is elevated,
/// and the negative control that proves HKLM is genuinely privileged is only meaningful when it is
/// not. Both are gated on this rather than silently passing in the wrong environment.
/// </summary>
internal static class TestHostElevation
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// True when the test process holds an administrator token. Mirrors the production check in
    /// <c>LANCommander.Launcher.Services.CurrentProcessInfo.IsElevated</c>.
    /// </summary>
    public static bool IsElevated
    {
        get
        {
            if (IsWindows)
            {
#pragma warning disable CA1416
                using var identity = WindowsIdentity.GetCurrent();

                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
            }

            return Environment.UserName == "root";
        }
    }
}

/// <summary>
/// A fact that only runs on Windows, elevated or not. Used for the HKCU twins of the HKLM tests,
/// which validate the script bodies and assertions on every Windows run.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!TestHostElevation.IsWindows)
            Skip = "Requires Windows — the registry provider only exists on Windows.";
    }
}

/// <summary>
/// A fact that only runs on an elevated Windows test host. Locally this means launching the test
/// run from an elevated shell; GitHub's Windows runners are already elevated, so these execute in CI.
/// </summary>
public sealed class ElevatedWindowsFactAttribute : FactAttribute
{
    public ElevatedWindowsFactAttribute()
    {
        if (!TestHostElevation.IsWindows)
            Skip = "Requires Windows — HKLM only exists on Windows.";
        else if (!TestHostElevation.IsElevated)
            Skip = "Requires an elevated test host — run the test process as administrator to exercise the HKLM write path.";
    }
}

/// <summary>
/// A fact that only runs on a non-elevated Windows test host. Used for the negative control that
/// proves writes to HKLM actually require elevation, so the elevated tests are not vacuous.
/// </summary>
public sealed class NonElevatedWindowsFactAttribute : FactAttribute
{
    public NonElevatedWindowsFactAttribute()
    {
        if (!TestHostElevation.IsWindows)
            Skip = "Requires Windows — HKLM only exists on Windows.";
        else if (TestHostElevation.IsElevated)
            Skip = "Requires a non-elevated test host — this is the negative control for the HKLM write tests.";
    }
}
