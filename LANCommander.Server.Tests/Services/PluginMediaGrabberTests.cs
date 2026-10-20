using LANCommander.SDK.Enums;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.MediaGrabbers;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Media sources plugins register through <see cref="IMediaGrabberExtension"/>, searched by the
/// server's composite media search alongside the built-in grabbers.
/// </summary>
[Collection("Application")]
public class PluginMediaGrabberTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task APluginsResultsComeBackUnderItsName()
    {
        var composite = Composite(new FakeGrabber("Plugin Source"));

        composite.GetGrabberNames(MediaType.Cover).ShouldContain("Plugin Source");
        composite.GetGrabberNames(MediaType.Video).ShouldNotContain("Plugin Source");

        var batches = await composite
            .SearchBatchesAsync(MediaType.Cover, "Quake", "Plugin Source", null, 0)
            .ToListAsync();

        var result = batches.ShouldHaveSingleItem().Results.ShouldHaveSingleItem();

        result.GrabberName.ShouldBe("Plugin Source");
        result.Id.ShouldBe("Quake-0");
        result.SourceUrl.ShouldBe("https://example.com/Quake.png");
        result.ThumbnailUrl.ShouldBe("https://example.com/Quake.png");
        result.Group.ShouldBe("Quake");
        result.MimeType.ShouldBe("image/png");
    }

    [Fact]
    public async Task ASourceWithoutPagingIsAskedForTheFirstPageOnly()
    {
        var grabber = new FakeGrabber("Plugin Source");
        var composite = Composite(grabber);

        var batches = await composite
            .SearchBatchesAsync(MediaType.Cover, "Quake", "Plugin Source", null, 1)
            .ToListAsync();

        batches.ShouldHaveSingleItem().Results.ShouldBeEmpty();
        grabber.Searches.ShouldBe(0);
    }

    [Fact]
    public void APluginCannotTakeTheNameOfABuiltInGrabber() =>
        Composite(new FakeGrabber("SteamGridDB")).GetGrabberNames().Count(name => name == "SteamGridDB").ShouldBe(1);

    private CompositeMediaGrabberService Composite(IMediaGrabberExtension extension) =>
        new(
            GetService<HqMediaGrabber>(),
            GetService<SteamGridDBMediaGrabber>(),
            GetService<YouTubeMediaGrabber>(),
            [new PluginMediaGrabbers([extension])]);

    private sealed class FakeGrabber(string name) : IMediaGrabberExtension
    {
        public int Searches { get; private set; }

        public string Name => name;

        public IReadOnlyCollection<MediaType> SupportedMediaTypes => [MediaType.Cover];

        public Task<IReadOnlyList<MediaGrabberItem>> SearchAsync(MediaType type, string keywords, int page, CancellationToken cancellationToken)
        {
            Searches++;

            return Task.FromResult<IReadOnlyList<MediaGrabberItem>>(
            [
                new MediaGrabberItem
                {
                    Id = $"{keywords}-{page}",
                    Type = type,
                    SourceUrl = $"https://example.com/{keywords}.png",
                    MimeType = "image/png",
                    Group = keywords,
                },
            ]);
        }
    }
}
