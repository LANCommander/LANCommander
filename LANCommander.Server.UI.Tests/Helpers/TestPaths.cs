using System.Reflection;

namespace LANCommander.Server.UI.Tests.Helpers;

/// <summary>
/// Source-tree locations, resolved from the <c>ProjectDirectory</c> assembly metadata the csproj
/// stamps in, so tests work no matter where the build output lands.
/// </summary>
public static class TestPaths
{
    public static string ProjectDirectory { get; } =
        typeof(TestPaths).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "ProjectDirectory")
            .Value!;

    public static string RepositoryRoot { get; } = Path.GetFullPath(Path.Combine(ProjectDirectory, ".."));

    public static string ServerProjectDirectory { get; } = Path.Combine(RepositoryRoot, "LANCommander.Server");

    public static string UIProjectDirectory { get; } = Path.Combine(RepositoryRoot, "LANCommander.Server.UI");
}
