using Semver;

namespace LANCommander.Server.Services;

public interface IVersionProvider
{
    SemVersion GetCurrentVersion();
}