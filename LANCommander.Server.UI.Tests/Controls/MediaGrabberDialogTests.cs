using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>
/// The media picker tracks each grabber it asked: a grabber shows as searching until its batch
/// lands, even when that batch is empty, and the footer counts how many have answered.
/// </summary>
public class MediaGrabberDialogTests : ControlsTestContext
{
    private readonly FakeGrabbers _grabbers = new("Alpha", "Beta");
    private readonly IRenderedComponent<ComponentHost> _host;

    public MediaGrabberDialogTests()
    {
        Services.AddSingleton<IMediaGrabberService>(_grabbers);

        _host = Render<ComponentHost>();
    }

    private void Open(MediaType type = MediaType.Cover, bool multiSelect = false) =>
        _ = Services.GetRequiredService<DialogService>().OpenAsync<MediaGrabberDialog, MediaGrabberOptions, MediaGrabberDownloadResult>("Download Cover", new MediaGrabberOptions
        {
            Type = type,
            Search = "Arena Blitz",
            MultiSelect = multiSelect,
        }, new DialogSettings { Picker = true, OkText = "Select", Subtitle = "Arena Blitz" });

    private static MediaGrabberResult Result(string id) => new()
    {
        Id = id,
        Type = MediaType.Cover,
        Group = "Arena Blitz",
        SourceUrl = $"https://example.test/{id}.png",
        ThumbnailUrl = $"https://example.test/{id}-thumb.png",
        MimeType = "image/png",
    };

    [Fact]
    public void EveryGrabberShowsAsSearching_UntilItAnswers()
    {
        Open();

        _host.WaitForAssertion(() =>
        {
            Assert.Equal(["Searching Alpha", "Searching Beta"], _host.FindAll(".media-grabber-searching").Select(s => s.TextContent.Trim()));
            Assert.Contains("0 of 2 providers answered", _host.Find(".media-grabber-footer-summary").TextContent);
        });

        _grabbers.Answer("Alpha", Result("a1"), Result("a2"));

        _host.WaitForAssertion(() =>
        {
            Assert.Equal(["Searching Beta"], _host.FindAll(".media-grabber-searching").Select(s => s.TextContent.Trim()));
            Assert.Equal(2, _host.FindAll(".media-picker-item").Count);
            Assert.Contains("2 results · 1 of 2 providers answered", _host.Find(".media-grabber-footer-summary").TextContent);
        });
    }

    [Fact]
    public void AGrabberWithNoResults_StillResolves()
    {
        Open();

        _grabbers.Answer("Alpha", Result("a1"));
        _grabbers.Answer("Beta");

        _host.WaitForAssertion(() =>
        {
            Assert.Empty(_host.FindAll(".media-grabber-searching"));
            Assert.Contains("1 result · 2 of 2 providers answered", _host.Find(".media-grabber-footer-summary").TextContent);
        });
    }

    [Fact]
    public void AFailedGrabber_CountsAsAnswered()
    {
        Open();

        _grabbers.Fail("Alpha");
        _grabbers.Answer("Beta", Result("b1"));

        _host.WaitForAssertion(() =>
        {
            Assert.Empty(_host.FindAll(".media-grabber-searching"));
            Assert.Contains("2 of 2 providers answered", _host.Find(".media-grabber-footer-summary").TextContent);
        });
    }

    [Fact]
    public void Groups_SayWhereTheyCameFrom_AndCountTheirResults()
    {
        Open();

        _grabbers.Answer("Alpha", Result("a1"), Result("a2"), Result("a3"));

        _host.WaitForAssertion(() =>
        {
            var header = _host.FindAll(".media-grabber-group-header").First(h => h.TextContent.Contains("via Alpha"));

            Assert.Equal("Arena Blitz", header.QuerySelector("h2")!.TextContent);
            Assert.Equal("3", header.QuerySelector(".media-grabber-group-count")!.TextContent.Trim());
        });
    }

    [Fact]
    public void Title_CarriesTheTypeChip_AndMultiSelect()
    {
        Open(MediaType.Video, multiSelect: true);

        _host.WaitForAssertion(() =>
        {
            var chips = _host.FindAll(".lc-dialog-title .lc-tag-caps").Select(t => t.TextContent.Trim()).ToList();

            Assert.Equal(["Video", "Multi-select"], chips);
            Assert.Equal("Arena Blitz", _host.Find(".lc-dialog-title .lc-dialog-subtitle").TextContent);
        });
    }

    [Fact]
    public void Selecting_CountsInTheFooter()
    {
        Open(multiSelect: true);

        _grabbers.Answer("Alpha", Result("a1"), Result("a2"));
        _grabbers.Answer("Beta");

        // Once every grabber has answered the results stop re-rendering, so the input found stays current
        _host.WaitForAssertion(() =>
        {
            Assert.Equal(2, _host.FindAll(".media-picker-item input").Count);
            Assert.Contains("2 of 2 providers answered", _host.Find(".media-grabber-footer-summary").TextContent);
        });

        _host.InvokeAsync(() => _host.FindAll(".media-picker-item input")[0].Change(true)).GetAwaiter().GetResult();

        _host.WaitForAssertion(() =>
        {
            Assert.Equal("1 selected", _host.Find(".media-grabber-footer-selected").TextContent.Trim());
            Assert.Equal("1 of 2", _host.Find(".media-grabber-group-count").TextContent.Trim());
        });
    }

    /// <summary>Grabbers whose answers the test hands out one at a time.</summary>
    private sealed class FakeGrabbers(params string[] names) : IMediaGrabberService
    {
        private readonly Channel<MediaGrabberBatch> _batches = Channel.CreateUnbounded<MediaGrabberBatch>();
        private int _answered;

        public string Name => "All";

        public MediaType[] SupportedMediaTypes => [MediaType.Cover, MediaType.Video];

        public IEnumerable<string> GetGrabberNames() => names;

        public IEnumerable<string> GetGrabberNames(MediaType type) => names;

        public void Answer(string grabber, params MediaGrabberResult[] results)
        {
            foreach (var result in results)
                result.GrabberName = grabber;

            Write(new MediaGrabberBatch { GrabberName = grabber, Results = results });
        }

        public void Fail(string grabber) => Write(new MediaGrabberBatch { GrabberName = grabber, Error = "Offline" });

        private void Write(MediaGrabberBatch batch)
        {
            _batches.Writer.TryWrite(batch);

            if (Interlocked.Increment(ref _answered) == names.Length)
                _batches.Writer.TryComplete();
        }

        public async IAsyncEnumerable<MediaGrabberBatch> SearchBatchesAsync(
            MediaType type, string keywords, string? grabberName, string? subProvider, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var batch in _batches.Reader.ReadAllAsync(cancellationToken))
                yield return batch;
        }

        public Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0) =>
            Task.FromResult<IEnumerable<MediaGrabberResult>>([]);

        public Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result) =>
            throw new NotSupportedException();
    }
}
