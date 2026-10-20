using System.Reflection;

namespace LANCommander.Server.UI.Fixtures;

public static class FixturesAssembly
{
    /// <summary>The assembly containing the fixture pages, for the server's router.</summary>
    public static Assembly Assembly => typeof(FixturesAssembly).Assembly;
}
