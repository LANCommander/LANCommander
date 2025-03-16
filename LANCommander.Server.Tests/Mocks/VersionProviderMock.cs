using LANCommander.Server.Services.Abstractions;
using Semver;

namespace LANCommander.Server.Tests.Mocks;

public class VersionProviderMock : IVersionProvider
{
    public static SemVersion Version { get; set; } = SemVersion.Parse("1.0.0");
    
    public SemVersion GetCurrentVersion()
    {
        return Version;
    }

    public static void SetVersion(string version)
    {
        Version = SemVersion.Parse(version);
    }
}