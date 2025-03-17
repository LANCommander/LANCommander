using System.Diagnostics;
using System.Reflection;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using Semver;

namespace LANCommander.Server.Providers;

public class VersionProvider : IVersionProvider
{
    public SemVersion GetCurrentVersion()
    {
        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        
        return SemVersion.Parse(version);
    }

    public ReleaseChannel GetReleaseChannel(SemVersion version)
    {
        if (version.IsRelease)
            return ReleaseChannel.Stable;
        
        if (version.IsPrerelease && version.PrereleaseIdentifiers.Any(pi => pi.Value == "nightly"))
            return ReleaseChannel.Nightly;
        
        if (version.IsPrerelease)
            return ReleaseChannel.Prerelease;
        
        throw new ArgumentException("Could not parse version number", nameof(version));
    }
}