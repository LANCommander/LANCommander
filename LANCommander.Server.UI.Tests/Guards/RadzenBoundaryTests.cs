using System.Text.RegularExpressions;
using LANCommander.Server.UI.Tests.Helpers;

namespace LANCommander.Server.UI.Tests.Guards;

/// <summary>
/// Radzen is an implementation detail of LANCommander.Server.UI. The server's pages and components
/// must only use the library's Controls, so swapping or upgrading the underlying component library
/// never touches application code.
/// </summary>
/// <remarks>
/// The library already references Radzen with <c>PrivateAssets="compile"</c>, which makes
/// <c>@using Radzen</c> a compile error in the server. These tests catch the remaining gaps: a
/// fully-qualified tag, a string such as a CSS class or script path, or someone adding a direct
/// package reference to the server.
/// </remarks>
public class RadzenBoundaryTests
{
    private static readonly Regex RadzenReference = new(@"\bRadzen\b|\brz-[a-z]", RegexOptions.Compiled);

    [Fact]
    public void ServerAssembly_DoesNotReferenceRadzen()
    {
        var references = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(references, name => name!.StartsWith("Radzen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServerSource_DoesNotMentionRadzen()
    {
        var offenders = Directory
            .EnumerateFiles(TestPaths.ServerProjectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, line, number: index + 1)))
            .Where(x => RadzenReference.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(TestPaths.RepositoryRoot, x.path)}:{x.number}: {x.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "LANCommander.Server must use LANCommander.Server.UI Controls instead of Radzen:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsSourceFile(string path) =>
        Path.GetExtension(path) is ".razor" or ".cs" or ".cshtml" or ".scss" or ".ts" || path.EndsWith(".csproj");

    private static bool IsBuildOutput(string path)
    {
        var relative = Path.GetRelativePath(TestPaths.ServerProjectDirectory, path);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        return first is "bin" or "obj" or "node_modules" or "wwwroot" || first.StartsWith("bin", StringComparison.OrdinalIgnoreCase);
    }
}
