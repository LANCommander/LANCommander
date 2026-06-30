using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using System;
using System.Linq;
using LANCommander.Server.Settings.Enums;
using Semver;

namespace LANCommander.Server.Tests.Mocks;

public class VersionProviderMock : IVersionProvider
{
    public static SemVersion Version { get; set; } = SemVersion.Parse("1.0.0");
    
    public SemVersion GetCurrentVersion()
    {
        return Version;
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

    public static void SetVersion(string version)
    {
        Version = SemVersion.Parse(version);
    }
}