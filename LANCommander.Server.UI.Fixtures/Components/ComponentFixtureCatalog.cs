namespace LANCommander.Server.UI.Fixtures.Components;

public static class ComponentFixtureCatalog
{
    private static readonly Lazy<IReadOnlyList<ComponentFixture>> _all = new(Discover);

    /// <summary>Every fixture from every <see cref="IComponentFixtureSet"/>, ordered by name.</summary>
    public static IReadOnlyList<ComponentFixture> All => _all.Value;

    public static ComponentFixture? Find(string name) =>
        All.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ComponentFixture> Discover() =>
        typeof(ComponentFixtureCatalog).Assembly
            .GetTypes()
            .Where(t => typeof(IComponentFixtureSet).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (IComponentFixtureSet)Activator.CreateInstance(t)!)
            .SelectMany(set => set.Fixtures)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
}
