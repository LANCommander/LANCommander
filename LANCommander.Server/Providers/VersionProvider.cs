using System.Diagnostics;
using System.Reflection;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using Semver;

namespace LANCommander.Server.Providers;

public class VersionProvider : IVersionProvider
{
    public SemVersion GetCurrentVersion()
    {
        var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        
        return SemVersion.Parse(version);
    }
}