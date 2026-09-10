using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

public class MediaServiceTests : IDisposable
{
    private readonly string _storagePath =
        Path.Combine(Path.GetTempPath(), $"MediaServiceTests-{Guid.NewGuid():N}");

    private static DatabaseContext CreateContext() =>
        new(
            NullLoggerFactory.Instance,
            new DbContextOptionsBuilder()
                .UseInMemoryDatabase($"MediaServiceTests-{Guid.NewGuid()}")
                .Options);

    private MediaService CreateSubject(DatabaseContext context)
    {
        var settings = new Settings.Settings();

        settings.Media.StoragePath = _storagePath;

        // Only the local database and file system are exercised, so the SDK client is never touched.
        return new MediaService(
            NullLogger<MediaService>.Instance,
            context,
            mediaClient: null!,
            TestSettingsProvider.Create(settings));
    }

    private Media CreateCachedMedia(Guid gameId, MediaType type)
    {
        var media = new Media
        {
            Id = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            GameId = gameId,
            Type = type,
            Crc32 = "abcd1234",
        };

        Directory.CreateDirectory(_storagePath);
        File.WriteAllText(Path.Combine(_storagePath, $"{media.FileId}-{media.Crc32}"), "cached");

        return media;
    }

    [Fact]
    public async Task RemoveMissingAsync_DeletesRecordAndFileForMediaTheServerNoLongerHas()
    {
        await using var context = CreateContext();

        var game = GameFactory.Make("Quake");
        var kept = CreateCachedMedia(game.Id, MediaType.Cover);
        var removed = CreateCachedMedia(game.Id, MediaType.Background);

        context.Games!.Add(game);
        context.Media!.AddRange(kept, removed);
        await context.SaveChangesAsync();

        var subject = CreateSubject(context);

        var count = await subject.RemoveMissingAsync(
            new Dictionary<Guid, HashSet<Guid>> { [game.Id] = [kept.Id] });

        count.ShouldBe(1);

        context.ChangeTracker.Clear();

        (await context.Media!.Select(m => m.Id).ToListAsync()).ShouldBe([kept.Id]);

        File.Exists(Path.Combine(_storagePath, $"{kept.FileId}-{kept.Crc32}")).ShouldBeTrue();
        File.Exists(Path.Combine(_storagePath, $"{removed.FileId}-{removed.Crc32}")).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveMissingAsync_LeavesMediaForGamesThatWereNotImported()
    {
        await using var context = CreateContext();

        var imported = GameFactory.Make("Quake");
        var untouched = GameFactory.Make("Doom");

        var importedMedia = CreateCachedMedia(imported.Id, MediaType.Cover);
        var untouchedMedia = CreateCachedMedia(untouched.Id, MediaType.Cover);

        context.Games!.AddRange(imported, untouched);
        context.Media!.AddRange(importedMedia, untouchedMedia);
        await context.SaveChangesAsync();

        var subject = CreateSubject(context);

        // Only the imported game was seen this run, so the other game's media is not authoritative.
        var count = await subject.RemoveMissingAsync(
            new Dictionary<Guid, HashSet<Guid>> { [imported.Id] = [importedMedia.Id] });

        count.ShouldBe(0);

        context.ChangeTracker.Clear();

        (await context.Media!.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task RemoveMissingAsync_IgnoresMediaThatIsNotAttachedToAGame()
    {
        await using var context = CreateContext();

        var game = GameFactory.Make("Quake");

        var avatar = CreateCachedMedia(gameId: Guid.Empty, MediaType.Avatar);
        avatar.GameId = null;

        context.Games!.Add(game);
        context.Media!.Add(avatar);
        await context.SaveChangesAsync();

        var subject = CreateSubject(context);

        var count = await subject.RemoveMissingAsync(
            new Dictionary<Guid, HashSet<Guid>> { [game.Id] = [] });

        count.ShouldBe(0);

        context.ChangeTracker.Clear();

        (await context.Media!.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveMissingAsync_DoesNothingWhenNoManifestsWereSeen()
    {
        await using var context = CreateContext();

        var game = GameFactory.Make("Quake");
        var media = CreateCachedMedia(game.Id, MediaType.Cover);

        context.Games!.Add(game);
        context.Media!.Add(media);
        await context.SaveChangesAsync();

        var subject = CreateSubject(context);

        (await subject.RemoveMissingAsync(new Dictionary<Guid, HashSet<Guid>>())).ShouldBe(0);

        context.ChangeTracker.Clear();

        (await context.Media!.CountAsync()).ShouldBe(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);
    }
}
