using System;
using System.IO;

namespace LANCommander.SDK.Helpers;

public static class EnvironmentHelper
{
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