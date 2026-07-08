using System;
using System.IO;
using System.Runtime.InteropServices;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Helpers;

public static class EnvironmentHelper
{
    /// <summary>
    /// Returns the <see cref="RuntimePlatform"/> the application is currently executing on.
    /// </summary>
    public static RuntimePlatform GetCurrentRuntime()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimePlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimePlatform.Linux;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimePlatform.macOS;

        return RuntimePlatform.None;
    }

    /// <summary>
    /// Determines whether the given set of platforms includes the current runtime. An unspecified
    /// (<see cref="RuntimePlatform.None"/>) value is treated as "runs everywhere" for backwards compatibility.
    /// </summary>
    public static bool SupportsCurrentRuntime(RuntimePlatform platforms)
    {
        return platforms == RuntimePlatform.None || platforms.HasFlag(GetCurrentRuntime());
    }

    public static bool IsRunningInContainer()
    {
        if (File.Exists("/.dockerenv"))
            return true;

        if (File.Exists("/proc/1/cgroup"))
        {
            var cgroup = File.ReadAllText("/proc/1/cgroup");
            if (cgroup.Contains("docker")
                || cgroup.Contains("kubepods")
                || cgroup.Contains("containerd")
                || cgroup.Contains("podman"))
                return true;
        }

        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            return true;

        return false;
    }
}