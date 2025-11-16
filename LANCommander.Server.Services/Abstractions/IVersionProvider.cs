using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using Semver;

namespace LANCommander.Server.Services.Abstractions;

public interface IVersionProvider
{
    SemVersion GetCurrentVersion();
    ReleaseChannel GetReleaseChannel(SemVersion version);
}