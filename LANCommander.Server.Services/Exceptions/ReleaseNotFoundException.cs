using Semver;

namespace LANCommander.Server.Services.Exceptions;

public class ReleaseNotFoundException(SemVersion version) : Exception
{
    public readonly SemVersion Version = version;
}