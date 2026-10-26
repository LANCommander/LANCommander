using LANCommander.Server.Plugins;
using Shouldly;

namespace LANCommander.Server.Tests.Services.PluginProviders;

[Collection("Application")]
public class GameVersionProviderTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    IGameVersionProvider Versions => GetService<IGameVersionProvider>();
    IGameProvider Games => GetService<IGameProvider>();
    IRedistributableProvider Redistributables => GetService<IRedistributableProvider>();

    [Fact]
    public async Task AnInitialVersionIsCreatedOnlyWhenNeeded()
    {
        var game = await Games.AddAsync(new GameInfo { Title = $"Initial version {Guid.NewGuid():N}" });
        var initial = await Versions.GetOrCreateLatestAsync(game.Id);

        initial.Version.ShouldBe("");
        (await Versions.GetOrCreateLatestAsync(game.Id)).Id.ShouldBe(initial.Id);
        (await Versions.GetAsync(game.Id)).ShouldHaveSingleItem();

        var draft = await Versions.CreateAsync(game.Id, "2.0");

        (await Versions.GetOrCreateLatestAsync(game.Id)).Id.ShouldBe(draft.Id);
        (await Versions.GetVersionAsync(draft.Id))!.Published.ShouldBeFalse();
        (await Versions.GetAsync(game.Id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task RedistributablesAndOptionsAreVersionScopedAndCarriedForward()
    {
        var game = await Games.AddAsync(new GameInfo { Title = $"Version ROM {Guid.NewGuid():N}" });
        var rom = await Redistributables.AddAsync(new RedistributableInfo { Name = $"ROM {Guid.NewGuid():N}", Notes = "Plugin provenance" });
        var runtime = await Redistributables.AddAsync(new RedistributableInfo { Name = $"Runtime {Guid.NewGuid():N}" });
        var first = await Versions.GetOrCreateLatestAsync(game.Id);

        await Versions.SetRedistributablesAsync(first.Id, [rom.Id, runtime.Id, rom.Id]);
        await Versions.SetRedistributableOptionsAsync(first.Id, rom.Id, """{"TargetFileName":"first.z64"}""");
        await Versions.SetRedistributableOptionsAsync(first.Id, runtime.Id, """{"Mode":"Silent"}""");

        var links = await Versions.GetRedistributablesAsync(first.Id);
        links.Count.ShouldBe(2);
        links.Single(link => link.Redistributable.Id == rom.Id).Redistributable.Notes.ShouldBe("Plugin provenance");
        (await Games.GetRedistributableOptionsAsync(game.Id, rom.Id)).ShouldBe("""{"TargetFileName":"first.z64"}""");

        var second = await Versions.CreateAsync(game.Id, "2.0");
        (await Versions.GetRedistributablesAsync(second.Id)).Single(link => link.Redistributable.Id == rom.Id)
            .Options.ShouldBe("""{"TargetFileName":"first.z64"}""");

        await Versions.SetRedistributableOptionsAsync(second.Id, rom.Id, """{"TargetFileName":"second.z64"}""");
        await Versions.SetRedistributablesAsync(second.Id, [runtime.Id]);

        (await Versions.GetRedistributablesAsync(second.Id)).ShouldHaveSingleItem().Options.ShouldBe("""{"Mode":"Silent"}""");
        (await Versions.GetRedistributablesAsync(first.Id)).Single(link => link.Redistributable.Id == rom.Id)
            .Options.ShouldBe("""{"TargetFileName":"first.z64"}""");

        await Versions.SetRedistributableOptionsAsync(second.Id, runtime.Id, null);
        (await Versions.GetRedistributablesAsync(second.Id)).ShouldHaveSingleItem().Options.ShouldBeNull();
        await Versions.SetRedistributablesAsync(second.Id, []);
        (await Versions.GetRedistributablesAsync(second.Id)).ShouldBeEmpty();
        (await Versions.GetRedistributablesAsync(first.Id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task MissingGamesVersionsAndRedistributablesAreRefusedWithoutChangingLinks()
    {
        var missing = Guid.NewGuid();

        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.GetOrCreateLatestAsync(missing));
        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.GetRedistributablesAsync(missing));
        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.SetRedistributablesAsync(missing, []));
        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.SetRedistributableOptionsAsync(missing, missing, "{}"));

        var game = await Games.AddAsync(new GameInfo { Title = $"Missing ROM {Guid.NewGuid():N}" });
        var version = await Versions.GetOrCreateLatestAsync(game.Id);
        var runtime = await Redistributables.AddAsync(new RedistributableInfo { Name = $"Kept {Guid.NewGuid():N}" });
        await Versions.SetRedistributablesAsync(version.Id, [runtime.Id]);

        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.SetRedistributablesAsync(version.Id, [missing]));
        await Should.ThrowAsync<KeyNotFoundException>(() => Versions.SetRedistributableOptionsAsync(version.Id, missing, "{}"));

        (await Versions.GetRedistributablesAsync(version.Id)).ShouldHaveSingleItem().Redistributable.Id.ShouldBe(runtime.Id);
    }

    [Fact]
    public async Task CancelledOperationsDoNotCreateVersionsOrModifyLinks()
    {
        var game = await Games.AddAsync(new GameInfo { Title = $"Cancelled ROM {Guid.NewGuid():N}" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => Versions.GetOrCreateLatestAsync(game.Id, cancellation.Token));
        (await Versions.GetAsync(game.Id)).ShouldBeEmpty();

        var version = await Versions.GetOrCreateLatestAsync(game.Id);
        await Should.ThrowAsync<OperationCanceledException>(() => Versions.GetRedistributablesAsync(version.Id, cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => Versions.SetRedistributablesAsync(version.Id, [], cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => Versions.SetRedistributableOptionsAsync(version.Id, Guid.NewGuid(), "{}", cancellation.Token));

        (await Versions.GetRedistributablesAsync(version.Id)).ShouldBeEmpty();
    }
}
