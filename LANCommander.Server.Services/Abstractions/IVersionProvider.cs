using Semver;

namespace LANCommander.Server.Services.Abstractions;

public interface IVersionProvider
{
    SemVersion GetCurrentVersion();
}