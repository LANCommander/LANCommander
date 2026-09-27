using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Fixtures.Components;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// Keeps the fixture gallery complete: every public control needs a fixture, so every control has
/// a visual baseline that catches regressions in it.
/// </summary>
public class FixtureCatalogTests
{
    /// <summary>
    /// Controls that render nothing visible of their own. Anything added here must say why.
    /// </summary>
    private static readonly HashSet<Type> Exempt =
    [
        typeof(Theme),          // Emits the theme stylesheet link
        typeof(ThemeScripts),   // Emits a script tag
        typeof(ComponentHost),  // Host for service-opened overlays; covered by the dialog/notification fixtures
    ];

    [Fact]
    public void FixtureNames_AreUnique()
    {
        var duplicates = ComponentFixtureCatalog.All
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void FixtureNames_AreControlDotState()
    {
        var malformed = ComponentFixtureCatalog.All
            .Select(f => f.Name)
            .Where(n => n.Split('.') is not [{ Length: > 0 }, { Length: > 0 }, ..] || n.Any(char.IsWhiteSpace))
            .ToList();

        Assert.Empty(malformed);
    }

    [Fact]
    public void EveryControl_HasAFixture()
    {
        var covered = ComponentFixtureCatalog.All.SelectMany(f => f.Covers).ToHashSet();

        var uncovered = typeof(Theme).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace == typeof(Theme).Namespace)
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t)
            .Where(t => !Exempt.Contains(t) && !covered.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(uncovered.Count == 0,
            "Controls without a fixture (add one to LANCommander.Server.UI.Fixtures/Components/Sets and list the control in Covers): "
            + string.Join(", ", uncovered));
    }
}
