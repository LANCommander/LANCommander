namespace LANCommander.Server.UI.Fixtures.Components;

/// <summary>
/// A group of fixtures, usually one per control. Implementations are discovered by reflection, so a
/// new set only needs to exist to show up in the gallery and the visual tests.
/// </summary>
public interface IComponentFixtureSet
{
    IEnumerable<ComponentFixture> Fixtures { get; }
}
