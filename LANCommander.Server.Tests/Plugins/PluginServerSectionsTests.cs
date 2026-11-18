using LANCommander.Server.Plugins;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ServerEntity = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Tests.Plugins;

/// <summary>
/// Plugins add pages, and a summary on the General page, to the server's own game servers through
/// <see cref="IServerSectionExtension"/>; only what can be shown is offered.
/// </summary>
public class PluginServerSectionsTests
{
    private sealed class Page : ComponentBase
    {
    }

    private sealed class FakeExtension(string id, string name = "Example") : IServerSectionExtension
    {
        public string Id => id;
        public string Name => name;

        public Func<ServerInfo, IReadOnlyList<ServerSection>> Sections { get; set; } = _ => [];
        public Func<ServerInfo, Type?> Overview { get; set; } = _ => null;
        public List<ServerInfo> Asked { get; } = [];

        public IReadOnlyList<ServerSection> GetSections(ServerInfo server)
        {
            Asked.Add(server);
            return Sections(server);
        }

        public Type? GetOverview(ServerInfo server) => Overview(server);
    }

    private static readonly ServerEntity Server = new()
    {
        Id = Guid.NewGuid(),
        Name = "Deathmatch",
        Engine = ServerEngine.Remote,
        Port = 27015,
        GameId = Guid.NewGuid(),
    };

    private static PluginServerSections Create(params IServerSectionExtension[] extensions) =>
        new(extensions, NullLogger<PluginServerSections>.Instance);

    [Fact]
    public void PagesAreOfferedByExtensionNameWithTheServerAsPluginsSeeIt()
    {
        var zebra = new FakeExtension("com.zebra", "Zebra") { Sections = _ => [new ServerSection("stripes", "Stripes", typeof(Page))] };
        var acme = new FakeExtension("com.acme", "Acme") { Sections = _ => [new ServerSection("players", "Players", typeof(Page))] };
        var quiet = new FakeExtension("com.quiet", "Quiet");

        var groups = Create(zebra, acme, quiet).GetSections(Server);

        groups.Select(g => g.Extension.Name).ShouldBe(["Acme", "Zebra"]);
        groups[0].Sections.ShouldHaveSingleItem().Slug.ShouldBe("players");

        var asked = acme.Asked.ShouldHaveSingleItem();
        asked.Id.ShouldBe(Server.Id);
        asked.Name.ShouldBe("Deathmatch");
        asked.Engine.ShouldBe(ServerHostEngine.Remote);
        asked.GameId.ShouldBe(Server.GameId);
    }

    [Fact]
    public void OnlyPagesThatCanBeShownAreOffered()
    {
        var extension = new FakeExtension("com.acme")
        {
            Sections = _ =>
            [
                new ServerSection("players", "Players", typeof(Page)),
                new ServerSection("Players", "Again", typeof(Page)),
                new ServerSection("../escape", "Escape", typeof(Page)),
                new ServerSection("text", "Text", typeof(string)),
                new ServerSection("config", " ", typeof(Page)),
            ],
        };

        var sections = Create(extension).GetSections(Server).ShouldHaveSingleItem().Sections;

        sections.Select(s => (s.Slug, s.Title)).ShouldBe([("players", "Players"), ("config", "config")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    public void ExtensionsWhoseIdsCantBeInAnAddressAreLeftOut(string id)
    {
        var extension = new FakeExtension(id) { Sections = _ => [new ServerSection("players", "Players", typeof(Page))] };

        Create(extension).GetSections(Server).ShouldBeEmpty();
    }

    [Fact]
    public void AnExtensionWhoseIdIsTakenIsLeftOut()
    {
        var first = new FakeExtension("com.acme", "First") { Sections = _ => [new ServerSection("a", "A", typeof(Page))] };
        var second = new FakeExtension("COM.ACME", "Second") { Sections = _ => [new ServerSection("b", "B", typeof(Page))] };

        Create(first, second).GetSections(Server).ShouldHaveSingleItem().Extension.Name.ShouldBe("First");
    }

    [Fact]
    public void AnExtensionThatFailsOffersNothingButTheOthersStill()
    {
        var broken = new FakeExtension("com.broken", "Broken")
        {
            Sections = _ => throw new InvalidOperationException("Broken"),
            Overview = _ => throw new InvalidOperationException("Broken"),
        };
        var working = new FakeExtension("com.working", "Working")
        {
            Sections = _ => [new ServerSection("a", "A", typeof(Page))],
            Overview = _ => typeof(Page),
        };

        var sections = Create(broken, working);

        sections.GetSections(Server).ShouldHaveSingleItem().Extension.Id.ShouldBe("com.working");
        sections.GetOverviews(Server).ShouldHaveSingleItem().Extension.Id.ShouldBe("com.working");
    }

    [Fact]
    public void PagesAreFoundByExtensionAndSlug()
    {
        var extension = new FakeExtension("com.acme") { Sections = _ => [new ServerSection("players", "Players", typeof(Page))] };
        var sections = Create(extension);

        var found = sections.Find(Server, "COM.ACME", "PLAYERS").ShouldNotBeNull();
        found.Section.Slug.ShouldBe("players");

        sections.Find(Server, "com.acme", "missing").ShouldBeNull();
        sections.Find(Server, "com.other", "players").ShouldBeNull();
    }

    [Fact]
    public void OnlyOverviewsThatAreComponentsAreShown()
    {
        var page = new FakeExtension("com.page", "Page") { Overview = _ => typeof(Page) };
        var text = new FakeExtension("com.text", "Text") { Overview = _ => typeof(string) };
        var none = new FakeExtension("com.none", "None");

        Create(page, text, none).GetOverviews(Server).ShouldHaveSingleItem().Component.ShouldBe(typeof(Page));
    }

    [Fact]
    public void ServersNotYetSavedHaveNoPages()
    {
        var extension = new FakeExtension("com.acme")
        {
            Sections = _ => [new ServerSection("players", "Players", typeof(Page))],
            Overview = _ => typeof(Page),
        };

        var unsaved = new ServerEntity { Name = "New" };
        var sections = Create(extension);

        sections.GetSections(unsaved).ShouldBeEmpty();
        sections.GetOverviews(unsaved).ShouldBeEmpty();
        extension.Asked.ShouldBeEmpty();
    }
}
