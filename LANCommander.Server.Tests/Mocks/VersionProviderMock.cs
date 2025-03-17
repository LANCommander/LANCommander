using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
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
        throw new NotImplementedException();
    }

    public static void SetVersion(string version)
    {
        Version = SemVersion.Parse(version);
    }
}