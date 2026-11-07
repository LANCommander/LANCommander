using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.PE;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class MediaServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    // Media is written through its own service rather than through the game, so the launcher's
    // change detection (which keys off Game.UpdatedOn) only sees media edits if the media service
    // touches the owning game.
    [Fact]
    public async Task AddingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Add Touch" });

        var stale = await MakeGameStaleAsync(game.Id);

        await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    [Fact]
    public async Task UpdatingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Update Touch" });

        var media = await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        var stale = await MakeGameStaleAsync(game.Id);

        media.Crc32 = "deadbeef";

        await mediaService.UpdateAsync(media);

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    [Fact]
    public async Task DeletingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Delete Touch" });

        var media = await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        var stale = await MakeGameStaleAsync(game.Id);

        await mediaService.DeleteAsync(media);

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    // Icons may be uploaded as a .exe, which the server mines for its icon resources. The executable must
    // never reach a storage location — only the .ico it yields.
    [Fact]
    public async Task ExtractingAnIconFromAnExecutableStoresAnIcoAndNotTheExecutable()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(NotepadPath))
            return;

        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddIconMediaAsync("Icon From Executable");

        await using var executable = File.OpenRead(NotepadPath);

        media = await mediaService.WriteIconFromExecutableAsync(media, executable);

        media.MimeType.ShouldBe("image/x-icon");

        var stored = await File.ReadAllBytesAsync(MediaService.GetMediaPath(media));

        IconFile.HasIconHeader(stored).ShouldBeTrue();
        PEIconExtractor.HasExecutableHeader(stored).ShouldBeFalse();
        ((long)stored.Length).ShouldBeLessThan(new FileInfo(NotepadPath).Length);
    }

    // ImageSharp cannot decode ICO, so without our own reader an extracted icon would have no preview.
    [Fact]
    public async Task ExtractingAnIconFromAnExecutableGeneratesAThumbnail()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(NotepadPath))
            return;

        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddIconMediaAsync("Icon Thumbnail From Executable");

        await using var executable = File.OpenRead(NotepadPath);

        media = await mediaService.WriteIconFromExecutableAsync(media, executable);

        mediaService.ThumbnailExists(media).ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractingAnIconFromANonExecutableIsRejected()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddIconMediaAsync("Icon From Junk");

        using var junk = new MemoryStream("this is not a portable executable"u8.ToArray());

        await Should.ThrowAsync<InvalidDataException>(
            () => mediaService.WriteIconFromExecutableAsync(media, junk));

        File.Exists(MediaService.GetMediaPath(media)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractingAnIconFromAnExecutableWithoutIconsIsRejected()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(NotepadDllPath))
            return;

        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddIconMediaAsync("Icon From Iconless Binary");

        await using var executable = File.OpenRead(NotepadDllPath);

        await Should.ThrowAsync<InvalidDataException>(
            () => mediaService.WriteIconFromExecutableAsync(media, executable));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 64)]
    [InlineData(64, 64)]
    [InlineData(65, 128)]
    [InlineData(400, 512)]
    [InlineData(3840, 3840)]
    [InlineData(10000, 3840)]
    public void SizedThumbnailRequestsSnapToAFixedLadder(int requested, int expected)
    {
        MediaService.SnapThumbnailSize(requested).ShouldBe(expected);
    }

    // Scaled displays ask for thumbnails at device-pixel size; the default thumbnail is half the source.
    [Fact]
    public async Task SizedThumbnailIsRenderedAtTheSnappedWidth()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddCoverMediaAsync("Sized Thumbnail", 1200, 1800);

        var path = await mediaService.GetThumbnailPathAsync(media, 400, 0);

        path.ShouldEndWith(".w512.Thumb");
        (await Image.IdentifyAsync(path)).Width.ShouldBe(512);
    }

    [Fact]
    public async Task SizedThumbnailNeverEnlargesPastTheSource()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddCoverMediaAsync("Sized Thumbnail Small Source", 300, 450);

        var info = await Image.IdentifyAsync(await mediaService.GetThumbnailPathAsync(media, 0, 1000));

        info.Width.ShouldBe(300);
        info.Height.ShouldBe(450);
    }

    [Fact]
    public async Task UnsizedThumbnailRequestServesTheDefaultThumbnail()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddCoverMediaAsync("Unsized Thumbnail", 600, 900);

        (await mediaService.GetThumbnailPathAsync(media, 0, 0)).ShouldBe(mediaService.GetThumbnailPath(media));
    }

    [Fact]
    public async Task RewritingTheSourceDiscardsSizedThumbnails()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddCoverMediaAsync("Sized Thumbnail Invalidation", 1200, 1800);

        var variant = await mediaService.GetThumbnailPathAsync(media, 512, 0);

        File.Exists(variant).ShouldBeTrue();

        using var replacement = CreateJpeg(800, 1200);

        await mediaService.WriteToFileAsync(media, replacement, overwrite: true);

        File.Exists(variant).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletingTheLocalFileDiscardsSizedThumbnails()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();
        var media = await AddCoverMediaAsync("Sized Thumbnail Delete", 1200, 1800);

        var variant = await mediaService.GetThumbnailPathAsync(media, 256, 0);

        mediaService.DeleteLocalMediaFile(media);

        File.Exists(variant).ShouldBeFalse();
    }

    // Browsers upload an APNG as image/png; the launcher and the APNG-to-video conversion both key
    // off image/apng, so the stored type has to come from the file.
    [Fact]
    public async Task AnimatedPngUploadedAsPngIsStoredAsApng()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();
        var game = await gameService.AddAsync(new Game { Title = "Animated Cover" });

        using var apng = CreatePng(120, 180, frames: 3);

        var media = await mediaService.WriteToFileAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            MimeType = "image/png",
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        }, apng);

        // With FFmpeg on the machine, the default settings go on to convert the APNG to MP4.
        media.MimeType.ShouldBeOneOf(MediaService.ApngMimeType, "video/mp4");
    }

    [Fact]
    public void AnimatedPngIsDetectedFromTheFile()
    {
        using var file = new TempFile(CreatePng(16, 16, frames: 2));

        MediaService.IsAnimatedPng(file.Path).ShouldBeTrue();
    }

    [Fact]
    public void StillPngIsNotAnimated()
    {
        using var file = new TempFile(CreatePng(16, 16, frames: 1));

        MediaService.IsAnimatedPng(file.Path).ShouldBeFalse();
    }

    [Fact]
    public void NonPngIsNotAnimated()
    {
        using var file = new TempFile(CreateJpeg(16, 16));

        MediaService.IsAnimatedPng(file.Path).ShouldBeFalse();
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public TempFile(Stream contents)
        {
            using (var fs = File.Create(Path))
                contents.CopyTo(fs);

            contents.Dispose();
        }

        public void Dispose() => File.Delete(Path);
    }

    /// <summary>A PNG; ImageSharp writes an APNG (acTL/fcTL/fdAT) when the image has more than one frame.</summary>
    private static MemoryStream CreatePng(int width, int height, int frames)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(40, 90, 160));

        for (var i = 1; i < frames; i++)
        {
            using var frame = new Image<Rgba32>(width, height, new Rgba32((byte)(40 + i * 60), 90, 160));
            image.Frames.AddFrame(frame.Frames.RootFrame);
        }

        var stream = new MemoryStream();

        image.SaveAsPng(stream);
        stream.Position = 0;

        return stream;
    }

    private async Task<Media> AddCoverMediaAsync(string gameTitle, int width, int height)
    {
        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = gameTitle });

        using var image = CreateJpeg(width, height);

        return await mediaService.WriteToFileAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            MimeType = "image/jpeg",
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        }, image);
    }

    private static MemoryStream CreateJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(40, 90, 160));

        var stream = new MemoryStream();

        image.SaveAsJpeg(stream);
        stream.Position = 0;

        return stream;
    }

    private static readonly string NotepadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");

    /// <summary>A system binary that carries resources but no icon group.</summary>
    private static readonly string NotepadDllPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");

    private async Task<Media> AddIconMediaAsync(string gameTitle)
    {
        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = gameTitle });

        return await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Icon,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });
    }

    /// <summary>
    /// Backdates the game's modified timestamp so the assertion does not depend on the wall clock
    /// advancing between two operations that run microseconds apart.
    /// </summary>
    private async Task<DateTime> MakeGameStaleAsync(Guid gameId)
    {
        var stale = DateTime.UtcNow.AddDays(-1);

        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        var game = await context.Games!.FirstAsync(g => g.Id == gameId);

        game.UpdatedOn = stale;

        await context.SaveChangesAsync();

        return stale;
    }

    private async Task<DateTime> GetGameUpdatedOnAsync(Guid gameId)
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Games!
            .AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => g.UpdatedOn)
            .FirstAsync();
    }
}
