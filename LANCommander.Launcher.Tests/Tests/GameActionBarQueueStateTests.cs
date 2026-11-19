using System;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// A game added behind another download is only in the queue, so the install button has to follow the queue
/// rather than the install call that put it there.
/// </summary>
public sealed class GameActionBarQueueStateTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly InstallService _installService;
    private readonly Game _game = new() { Id = Guid.NewGuid(), Title = "Natural Selection" };

    public GameActionBarQueueStateTests()
    {
        var eventBus = new PluginEventBus(NullLogger<PluginEventBus>.Instance);

        // Only the queue and its change notifications are exercised
        var gameClient = new GameClient(
            NullLogger<GameClient>.Instance, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, eventBus, []);
        var redistributableClient = new RedistributableClient(NullLogger<RedistributableClient>.Instance, null!, null!, null!, null!);
        var toolClient = new ToolClient(NullLogger<ToolClient>.Instance, null!, null!, null!);

        _installService = new InstallService(
            NullLogger<InstallService>.Instance, null!, gameClient, redistributableClient, toolClient, null!, eventBus);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_installService);
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Install_button_follows_the_game_through_the_queue()
    {
        var actionBar = new GameActionBarViewModel(_services) { GameId = _game.Id };
        actionBar.FollowInstallQueue();

        Assert.True(actionBar.ShowInstallLabel);
        Assert.True(actionBar.CanInstall);

        var queueItem = new InstallQueueGame(_game) { Status = InstallStatus.Queued };
        _installService.Queue.Add(queueItem);
        Dispatcher.UIThread.RunJobs();

        Assert.True(actionBar.IsQueued);
        Assert.False(actionBar.ShowInstallLabel);
        Assert.False(actionBar.ShowInstallingLabel);
        Assert.False(actionBar.CanInstall);

        queueItem.Status = InstallStatus.Downloading;
        _installService.Queue.Move(0, 0);
        Dispatcher.UIThread.RunJobs();

        Assert.False(actionBar.IsQueued);
        Assert.True(actionBar.ShowInstallingLabel);
        Assert.False(actionBar.CanInstall);

        queueItem.Status = InstallStatus.Failed;
        _installService.Queue.Move(0, 0);
        Dispatcher.UIThread.RunJobs();

        Assert.True(actionBar.ShowInstallLabel);
        Assert.True(actionBar.CanInstall);
    }

    [AvaloniaFact]
    public void Leaving_the_page_stops_following_the_queue()
    {
        var actionBar = new GameActionBarViewModel(_services) { GameId = _game.Id };
        actionBar.FollowInstallQueue();
        actionBar.StopRunningCheck();

        _installService.Queue.Add(new InstallQueueGame(_game) { Status = InstallStatus.Queued });
        Dispatcher.UIThread.RunJobs();

        Assert.False(actionBar.IsQueued);
    }

    [AvaloniaFact]
    public void Another_games_queue_item_leaves_the_button_alone()
    {
        var actionBar = new GameActionBarViewModel(_services) { GameId = _game.Id };
        actionBar.FollowInstallQueue();

        _installService.Queue.Add(new InstallQueueGame(new Game { Id = Guid.NewGuid(), Title = "Half-Life" }) { Status = InstallStatus.Queued });
        Dispatcher.UIThread.RunJobs();

        Assert.False(actionBar.IsQueued);
        Assert.True(actionBar.ShowInstallLabel);
    }
}
