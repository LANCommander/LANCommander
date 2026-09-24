#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.ViewModels;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using LiveChartsCore.SkiaSharpView;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The downloads page, and the footer's transfer bar while something downloads.</summary>
public static class DownloadFixtures
{
    private const long GB = 1024L * 1024 * 1024;

    private const long Speed = 18_400_000;

    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("Downloads.Empty", "Nothing queued", context =>
            context.ShellWindow(context.Shell.DownloadQueue)),

        new("Downloads.Queue", "One download running, others waiting, some finished", context =>
        {
            SeedQueue(context.Shell.DownloadQueue, expanded: false);

            return context.ShellWindow(context.Shell.DownloadQueue);
        }),

        new("Downloads.Expanded", "The running download expanded to its task timeline", context =>
        {
            SeedQueue(context.Shell.DownloadQueue, expanded: true);

            return context.ShellWindow(context.Shell.DownloadQueue);
        }) { Height = 1000 },

        new("Downloads.Finished", "Only finished installs left, one of them failed", context =>
        {
            context.Shell.DownloadQueue.SeedForFixture(
            [
                Item(FixtureGames.Quake3, InstallStatus.Complete, 480_000_000),
                Item(FixtureGames.Doom2, InstallStatus.Complete, 32_000_000),
                Item(FixtureGames.StarCraft, InstallStatus.Failed, 920_000_000),
            ]);

            return context.ShellWindow(context.Shell.DownloadQueue);
        }),

        new("Shell.Downloading", "The library, with the footer showing a download's progress", context =>
        {
            SeedQueue(context.Shell.DownloadQueue, expanded: false);

            var library = context.Shell.LibraryViewModel;
            library.SelectedViewType = Settings.Enums.GameViewType.Grid;
            library.SeedFixture(FixtureGames.Library.Select(g => g.ToItem(libraryBadge: false)));

            return context.ShellWindow(library);
        }),
    ];

    internal static void SeedQueue(DownloadQueueViewModel queue, bool expanded)
    {
        var active = Item(FixtureGames.Battlefield1942, InstallStatus.Downloading, (long)(4.7 * GB), (long)(1.9 * GB));

        active.IsExpanded = expanded;

        // The chart eases between values on its own clock, which a capture could land partway through.
        ((LineSeries<double>)active.SpeedSeries[0]).AnimationsSpeed = TimeSpan.Zero;

        // Download under way, the rest waiting.
        active.Tasks[0].Status = InstallTaskStatus.Running;
        active.Tasks[0].Progress = active.Progress;

        // A steady transfer, so the speed chart has something level to draw.
        for (var i = 0; i < 60; i++)
            active.UpdateProgress(InstallStatus.Downloading, active.Progress, Speed + (i % 5) * 400_000, active.BytesDownloaded, active.TotalBytes);

        queue.SeedForFixture(
            [
                active,
                Item(FixtureGames.Warcraft3, InstallStatus.Queued, (long)(1.25 * GB)),
                Item(FixtureGames.AgeOfEmpires2, InstallStatus.Queued, 380_000_000, update: true),
                Item(FixtureGames.Quake3, InstallStatus.Complete, 480_000_000),
                Item(FixtureGames.StarCraft, InstallStatus.Failed, 920_000_000),
            ],
            new InstallProgress
            {
                Game = new SDK.Models.Game { Id = active.Id, Title = active.Title },
                Title = active.Title,
                Status = InstallStatus.Downloading,
                TransferSpeed = Speed,
                BytesTransferred = active.BytesDownloaded,
                TotalBytes = active.TotalBytes,
            });
    }

    private static InstallQueueItemViewModel Item(FixtureGame game, InstallStatus status, long totalBytes, long downloaded = 0, bool update = false)
    {
        var finished = status is InstallStatus.Complete or InstallStatus.Failed;

        var source = new InstallQueueGame(new SDK.Models.Game { Id = game.Id, Title = game.Title })
        {
            Status = status,
            IsUpdate = update,
            TotalBytes = totalBytes,
            BytesDownloaded = status == InstallStatus.Complete ? totalBytes : downloaded,
            CompletedOn = finished ? FixtureContext.Now.AddMinutes(-18) : null,
            Tasks = DownloadQueueViewModel.FixtureTasks(),
        };

        var item = new InstallQueueItemViewModel(source)
        {
            CoverPath = game.CoverPath,
            HasCover = true,
            IconPath = game.IconPath,
            HasIcon = true,
        };

        if (status == InstallStatus.Complete)
            foreach (var task in item.Tasks)
                task.Status = InstallTaskStatus.Completed;

        if (status == InstallStatus.Failed)
        {
            item.Tasks[0].Status = InstallTaskStatus.Completed;
            item.Tasks[1].Status = InstallTaskStatus.Completed;
            item.Tasks[2].Status = InstallTaskStatus.Failed;
            item.Tasks[2].ErrorMessage = "The install script exited with code 1.";
        }

        return item;
    }
}
#endif
