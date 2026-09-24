#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Debug-only fixture that fills the download queue with fake items in every state so the footer
/// transfer bar, rail badge and Downloads page can be styled without installing anything.
/// Set <c>LANCOMMANDER_FAKE_DOWNLOADS=1</c> before starting a Debug build. Nothing touches the
/// install service or the disk; titles and art come from games already in the local library.
/// </summary>
public partial class DownloadQueueViewModel
{
    public const string FixtureEnvironmentVariable = "LANCOMMANDER_FAKE_DOWNLOADS";

    private static readonly TimeSpan FixtureTick = TimeSpan.FromMilliseconds(250);

    private DispatcherTimer? _fixtureTimer;
    private long _fixtureBytes;
    private readonly Random _fixtureRandom = new(1);

    public static bool IsFixtureRequested =>
        Environment.GetEnvironmentVariable(FixtureEnvironmentVariable) is "1" or "true";

    private async Task SeedFixtureAsync()
    {
        _fixtureActive = true;
        _logger.LogWarning("Download queue fixture active ({Variable}); the real install queue is ignored", FixtureEnvironmentVariable);

        using var scope = _serviceProvider.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

        var games = (await gameService.GetAsync())
            .OrderBy(g => g.Title)
            .Take(5)
            .ToList();

        var specs = new (InstallStatus Status, bool IsUpdate, long TotalBytes)[]
        {
            (InstallStatus.Downloading, false, 4_700_000_000),
            (InstallStatus.Queued,      false, 1_250_000_000),
            (InstallStatus.Queued,      true,    380_000_000),
            (InstallStatus.Complete,    false, 2_100_000_000),
            (InstallStatus.Failed,      false,   920_000_000),
        };

        QueueItems.Clear();

        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var game = i < games.Count ? games[i] : null;

            // No Media on the stand-in game, so the view model won't try to download art;
            // local art is attached below instead.
            var source = new InstallQueueGame(new Game
            {
                Id = game?.Id ?? Guid.NewGuid(),
                Title = game?.Title ?? $"Fixture Game {i + 1}",
            })
            {
                Status = spec.Status,
                IsUpdate = spec.IsUpdate,
                QueuedOn = DateTime.Now.AddMinutes(-10 + i),
                CompletedOn = spec.Status == InstallStatus.Complete ? DateTime.Now.AddMinutes(-3) : null,
                TotalBytes = spec.TotalBytes,
                BytesDownloaded = spec.Status == InstallStatus.Complete ? spec.TotalBytes : 0,
                Tasks = FixtureTasks(),
            };

            var vm = new InstallQueueItemViewModel(source);

            if (game != null)
            {
                var icon = await mediaService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == MediaType.Icon);
                if (icon != null && mediaService.FileExists(icon))
                {
                    vm.IconPath = mediaService.GetImagePath(icon);
                    vm.HasIcon = true;
                }

                var cover = await mediaService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == MediaType.Cover);
                if (cover != null && mediaService.FileExists(cover))
                {
                    vm.CoverPath = mediaService.GetImagePath(cover);
                    vm.HasCover = true;
                }
            }

            QueueItems.Add(vm);
        }

        UpdateStateFlags();

        _fixtureBytes = 0;
        _fixtureTimer = new DispatcherTimer { Interval = FixtureTick };
        _fixtureTimer.Tick += (_, _) => AdvanceFixture();
        _fixtureTimer.Start();
    }

    /// <summary>
    /// Fills the queue for a visual fixture (see <see cref="Fixtures.FixtureContext"/>) and, when
    /// given, reports progress for the active item through the same handler real installs use.
    /// Unlike <see cref="SeedFixtureAsync"/> nothing moves afterwards.
    /// </summary>
    internal void SeedForFixture(IEnumerable<InstallQueueItemViewModel> items, InstallProgress? progress = null)
    {
        _fixtureActive = true;

        QueueItems.Clear();

        foreach (var item in items)
            QueueItems.Add(item);

        UpdateStateFlags();

        if (progress != null)
            _ = OnProgress(progress);
    }

    internal static List<InstallTaskDefinition> FixtureTasks() => new()
    {
        new() { Type = InstallTaskType.DownloadAndExtract, Title = "Download and extract", Order = 0, ReportsProgress = true, IsCritical = true },
        new() { Type = InstallTaskType.WriteManifest,      Title = "Write manifest",       Order = 1 },
        new() { Type = InstallTaskType.RunInstallScript,   Title = "Run install script",   Order = 2 },
    };

    /// <summary>
    /// Moves the active item along through the same handlers real installs use, looping back to
    /// the start when it finishes so the transfer bar never goes idle.
    /// </summary>
    private void AdvanceFixture()
    {
        var active = QueueItems.FirstOrDefault(i => i.IsActive);
        if (active == null)
            return;

        // 14–24 MB/s with some jitter, so the speed readout and chart move
        var speed = 14_000_000 + _fixtureRandom.Next(0, 10_000_000);
        _fixtureBytes += (long)(speed * FixtureTick.TotalSeconds);

        if (_fixtureBytes >= active.TotalBytes)
            _fixtureBytes = 0;

        _ = OnProgress(new InstallProgress
        {
            Game = new Game { Id = active.Id, Title = active.Title },
            Title = active.Title,
            Status = InstallStatus.Downloading,
            TransferSpeed = speed,
            BytesTransferred = _fixtureBytes,
            TotalBytes = active.TotalBytes,
        });

        var download = active.Tasks.FirstOrDefault();
        if (download != null)
        {
            _ = OnTaskProgressUpdate(new InstallTaskProgress
            {
                QueueItemId = active.Id,
                TaskId = download.Id,
                TaskType = InstallTaskType.DownloadAndExtract,
                TaskTitle = "Download and extract",
                TaskStatus = InstallTaskStatus.Running,
                Progress = _fixtureBytes / (float)active.TotalBytes,
                BytesTransferred = _fixtureBytes,
                TotalBytes = active.TotalBytes,
                TransferSpeed = speed,
            });
        }
    }
}
#endif
