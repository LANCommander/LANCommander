using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GameService = LANCommander.Launcher.Services.GameService;

namespace LANCommander.Launcher.Services.Protocol;

/// <summary>
/// Opens <c>lancommander://</c> links, whichever way they arrive: on the command line of a cold
/// start, from a second launcher process over the single-instance pipe, or as a macOS open-URL
/// event. A link waits until the library is showing, so one opened at start-up, or while the user
/// is still signing in, runs once they reach it.
/// </summary>
public sealed class ProtocolRequestHandler(IServiceProvider services, ILogger<ProtocolRequestHandler> logger)
{
    private readonly Queue<string> _pending = new();
    private MainWindowViewModel? _mainWindow;
    private Action? _restoreWindow;

    /// <summary>Starts handling links once the main window exists. Links queued before this wait for it.</summary>
    public void Attach(MainWindowViewModel mainWindow, Action restoreWindow)
    {
        _mainWindow = mainWindow;
        _restoreWindow = restoreWindow;

        mainWindow.PropertyChanged += OnMainWindowPropertyChanged;

        Dispatcher.UIThread.Post(Drain);
    }

    public void Enqueue(string url)
    {
        logger.LogInformation("Received link {Url}", url);

        lock (_pending)
            _pending.Enqueue(url);

        Dispatcher.UIThread.Post(() =>
        {
            // Surface the window even while the link waits for sign-in, so the user sees why nothing happened yet
            _restoreWindow?.Invoke();
            Drain();
        });
    }

    private void OnMainWindowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsShellActive))
            Dispatcher.UIThread.Post(Drain);
    }

    private void Drain()
    {
        if (_mainWindow is not { IsShellActive: true })
            return;

        List<string> urls;

        lock (_pending)
        {
            urls = _pending.ToList();
            _pending.Clear();
        }

        // Each link runs on its own: a play link holds its task until the game exits
        foreach (var url in urls)
            _ = OpenAsync(url, _mainWindow.ShellViewModel);
    }

    private async Task OpenAsync(string url, ShellViewModel shell)
    {
        try
        {
            if (!ProtocolRequest.TryParse(url, out var request, out var error))
            {
                logger.LogWarning("Could not open link {Url}: {Error}", url, error);
                await AlertOverlay.ShowAsync("Couldn't Open Link", error!);
                return;
            }

            await shell.NavigateToGameByIdAsync(request!.GameId);

            if (request.Kind != ProtocolRequestKind.ViewGame)
                await PlayAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not open link {Url}", url);
            await AlertOverlay.ShowAsync("Failed to Launch", ex.Message);
        }
    }

    private async Task PlayAsync(ProtocolRequest request)
    {
        using var scope = services.CreateScope();

        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

        var game = await gameService.GetAsync(request.GameId);

        if (game is not { Installed: true })
        {
            // The game's page is already open, with its Install button
            await AlertOverlay.ShowAsync("Not Installed",
                $"{game?.Title ?? "This game"} isn't installed. Install it, then open the link again.");
            return;
        }

        if (gameClient.IsRunning(game.Id))
        {
            logger.LogInformation("Ignoring play link for {GameTitle}: it is already running", game.Title);
            return;
        }

        var actions = (await gameClient.GetActionsAsync(game.InstallDirectory, game.Id))?.ToList() ?? [];
        var action = ProtocolActions.Select(actions, request.Kind, request.ActionId);

        // A play link that doesn't name an action asks which, as the Play button does, when the game
        // has more than one primary action
        if (request.Kind == ProtocolRequestKind.PlayGame && !request.ActionId.HasValue)
        {
            var (chosen, cancelled) = await GameActionsOverlay.ChoosePlayActionAsync(game.Title, actions);

            if (cancelled)
                return;

            action = chosen;
        }

        if (action == null)
        {
            var joining = request.Kind == ProtocolRequestKind.JoinGame;

            await AlertOverlay.ShowAsync(joining ? "Couldn't Join Game" : "Couldn't Start Game", request.ActionId.HasValue
                ? $"The link asks for {(joining ? "a Join action" : "an action")} {game.Title} doesn't have. It may have been made for another version of the game."
                : joining
                    ? $"{game.Title} has no Join action that can run on this computer."
                    : $"{game.Title} has no action that can run on this computer.");
            return;
        }

        var unused = ProtocolActions.UnusedParameters(action, request.Parameters);

        if (unused.Count > 0)
            logger.LogInformation("Link parameters {Parameters} have no placeholder in {ActionName}'s arguments", unused, action.Name);

        logger.LogInformation("Running action {ActionName} for game {GameId} from a link", action.Name, game.Id);

        await gameService.Run(game, ProtocolActions.ApplyParameters(action, request.Parameters));
    }
}
