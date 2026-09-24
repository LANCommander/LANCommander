using LANCommander.SDK.Enums;
using LANCommander.Server.Services.MediaGrabbers;
using Shouldly;
using HqModels = LANCommander.HQ.SDK.Models;

namespace LANCommander.Server.Tests.Services;

public class HqMediaGrabberTests
{
    private const string GameId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string MediaId = "6ba7b810-9dad-11d1-80b4-00c04fd430c8";

    [Fact]
    public void TheMediaIdIsTakenFromTheDtoWhenHqSendsOne()
    {
        var media = new HqModels.MediaDto
        {
            Id = Guid.Parse(MediaId),
            Url = $"https://hq.lancommander.app/Games/{GameId}/media/{Guid.Empty}",
        };

        HqMediaGrabber.ResolveMediaId(media).ShouldBe(Guid.Parse(MediaId));
    }

    [Fact]
    public void TheMediaIdFallsBackToTheServedUrl()
    {
        // HQ caches each game payload for 24 hours, so responses mapped before the server started
        // populating Id keep arriving with it empty for a while. Url has always pointed at the
        // by-id route, so it is the reliable source during that window.
        var media = new HqModels.MediaDto
        {
            Url = $"https://hq.lancommander.app/Games/{GameId}/media/{MediaId}",
        };

        HqMediaGrabber.ResolveMediaId(media).ShouldBe(Guid.Parse(MediaId));
    }

    [Theory]
    [InlineData("https://hq.lancommander.app/Games/x/media/" + MediaId + "?locale=en")]
    [InlineData("https://hq.lancommander.app/Games/x/media/" + MediaId + "#fragment")]
    [InlineData("https://hq.lancommander.app/Games/x/media/" + MediaId + "/")]
    public void TheServedUrlFallbackIgnoresTrailingNoise(string url)
    {
        HqMediaGrabber.ResolveMediaId(new HqModels.MediaDto { Url = url })
            .ShouldBe(Guid.Parse(MediaId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://hq.lancommander.app/Games/x/media/4")]
    public void AnUnresolvableMediaIdIsEmpty(string? url)
    {
        // Empty is meaningful: DownloadAsync uses it to fall back to the by-type route rather
        // than sending HQ a request it cannot answer.
        HqMediaGrabber.ResolveMediaId(new HqModels.MediaDto { Url = url }).ShouldBe(Guid.Empty);
    }

    [Theory]
    [InlineData(
        "https://images.igdb.com/igdb/image/upload/t_screenshot_big/sc1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_screenshot_med/sc1abc.jpg")]
    [InlineData(
        "https://images.igdb.com/igdb/image/upload/t_screenshot_huge/sc1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_screenshot_med/sc1abc.jpg")]
    [InlineData(
        "https://images.igdb.com/igdb/image/upload/t_cover_big/co1abc.png",
        "https://images.igdb.com/igdb/image/upload/t_cover_small/co1abc.png")]
    public void IgdbImagesAreRequestedAtThumbnailSize(string source, string expected)
    {
        HqMediaGrabber.Downscale(source).ShouldBe(expected);
    }

    // HQ stores IGDB covers and screenshots at t_1080p so they hold up on scaled displays; the token
    // doesn't say what the image is, so the picker preview takes that from the media type.
    [Theory]
    [InlineData(MediaType.Cover,
        "https://images.igdb.com/igdb/image/upload/t_1080p/co1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_cover_small/co1abc.jpg")]
    [InlineData(MediaType.Screenshot,
        "https://images.igdb.com/igdb/image/upload/t_1080p/sc1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_screenshot_med/sc1abc.jpg")]
    [InlineData(MediaType.Screenshot,
        "https://images.igdb.com/igdb/image/upload/t_original/sc1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_screenshot_med/sc1abc.jpg")]
    [InlineData(MediaType.Background,
        "https://images.igdb.com/igdb/image/upload/t_1080p/ar1abc.jpg",
        "https://images.igdb.com/igdb/image/upload/t_1080p/ar1abc.jpg")]
    public void SizeOnlyIgdbTokensAreDownscaledByMediaType(MediaType type, string source, string expected)
    {
        HqMediaGrabber.Downscale(source, type).ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://images.igdb.com/igdb/image/upload/t_thumb/co1abc.png")]
    [InlineData("https://cdn.example.com/igdb/image/upload/t_screenshot_big/sc1abc.jpg")]
    [InlineData("https://steamcdn.example.com/apps/1234/ss_beef.jpg")]
    [InlineData("not-a-url")]
    public void AnythingOtherThanARecognisedIgdbUrlIsLeftAlone(string source)
    {
        HqMediaGrabber.Downscale(source).ShouldBe(source);
    }

    [Fact]
    public void ScreenshotsUseTheirOwnImageRatherThanTheGameCover()
    {
        // The bug this guards: handing every media item the game's cover made all six of a
        // game's screenshots render as the same tile in the picker.
        var media = new HqModels.MediaDto
        {
            Type = HqModels.MediaType.Screenshot,
            SourceUrl = "https://images.igdb.com/igdb/image/upload/t_screenshot_big/sc1abc.jpg",
        };

        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Screenshot, media, "https://cover.example/co.jpg")
            .ShouldBe("https://images.igdb.com/igdb/image/upload/t_screenshot_med/sc1abc.jpg");
    }

    [Fact]
    public void MediaWithNoSourceUrlFallsBackToTheGameCover()
    {
        var media = new HqModels.MediaDto { Type = HqModels.MediaType.Screenshot };

        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Screenshot, media, "https://cover.example/co.jpg")
            .ShouldBe("https://cover.example/co.jpg");
    }

    [Fact]
    public void VideosShowTheGameCoverBecauseTheirSourceIsNotAnImage()
    {
        var media = new HqModels.MediaDto
        {
            Type = HqModels.MediaType.Video,
            SourceUrl = "https://videos.example/trailer.mp4",
        };

        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Video, media, "https://cover.example/co.jpg")
            .ShouldBe("https://cover.example/co.jpg");
    }

    [Fact]
    public void ManualsShowThePdfPlaceholder()
    {
        var media = new HqModels.MediaDto
        {
            Type = HqModels.MediaType.Manual,
            SourceUrl = "https://manuals.example/manual.pdf",
        };

        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Manual, media, "https://cover.example/co.jpg")
            .ShouldBe("/static/pdf.png");
    }

    [Fact]
    public void AThumbnailIsNeverNull()
    {
        // ImagePicker binds the value straight into an img src, and MediaGrabberResult.ThumbnailUrl
        // is non-nullable.
        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Screenshot, new HqModels.MediaDto(), null)
            .ShouldBe(string.Empty);

        HqMediaGrabber.ResolveThumbnailUrl(MediaType.Video, new HqModels.MediaDto(), null)
            .ShouldBe(string.Empty);
    }
}
