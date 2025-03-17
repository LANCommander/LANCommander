using LANCommander.Server.Services.Models;
using Semver;

namespace LANCommander.Server.Services.Abstractions;

public interface IVersionProvider
{
    SemVersion GetCurrentVersion();
    ReleaseChannel GetReleaseChannel(SemVersion version);
}