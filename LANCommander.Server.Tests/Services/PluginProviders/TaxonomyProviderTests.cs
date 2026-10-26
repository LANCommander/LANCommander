using LANCommander.Server.Plugins;
using Shouldly;

namespace LANCommander.Server.Tests.Services.PluginProviders;

[Collection("Application")]
public class TaxonomyProviderTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Theory]
    [InlineData(TaxonomyKind.Category)]
    [InlineData(TaxonomyKind.Collection)]
    [InlineData(TaxonomyKind.Company)]
    [InlineData(TaxonomyKind.Engine)]
    [InlineData(TaxonomyKind.Genre)]
    [InlineData(TaxonomyKind.Platform)]
    [InlineData(TaxonomyKind.Tag)]
    public async Task EveryKindCanBeAddedFoundRenamedAndDeleted(TaxonomyKind kind)
    {
        var provider = GetService<ITaxonomyProvider>();
        var name = $"Plugin {kind} {Guid.NewGuid():N}";

        var added = await provider.GetOrAddAsync(kind, name);

        added.Kind.ShouldBe(kind);
        added.Name.ShouldBe(name);

        // Names match whatever their case, so a second lookup finds the same record
        (await provider.GetOrAddAsync(kind, name.ToUpperInvariant())).Id.ShouldBe(added.Id);
        (await provider.GetAsync(kind)).ShouldContain(record => record.Id == added.Id);

        var renamed = await provider.RenameAsync(kind, added.Id, name + " renamed");

        (await provider.GetAsync(kind, added.Id))!.Name.ShouldBe(renamed.Name);

        await provider.DeleteAsync(kind, added.Id);

        (await provider.GetAsync(kind, added.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task RenamingAMissingRecordThrows() =>
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            GetService<ITaxonomyProvider>().RenameAsync(TaxonomyKind.Genre, Guid.NewGuid(), "Nothing"));
}
