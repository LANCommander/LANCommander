using System.Reflection;
using Semver;

namespace LANCommander.SDK.Helpers;

public static class VersionHelper
{
    public static SemVersion GetCurrentVersion()
    {
        return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
    }
}