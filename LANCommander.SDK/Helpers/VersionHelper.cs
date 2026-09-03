using System;
using System.Reflection;
using Semver;

namespace LANCommander.SDK.Helpers;

public static class VersionHelper
{
    // The executing assembly's version never changes during the process lifetime, but computing
    // it involves reflection (Assembly.GetExecutingAssembly().GetName()) which is surprisingly
    // costly when called repeatedly (e.g. once or twice per API request). Cache it once.
    private static readonly SemVersion _currentVersion =
        SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0));

    public static SemVersion GetCurrentVersion() => _currentVersion;
}