using LANCommander.Interposer;
using LANCommander.Interposer.Events;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// Passes what a game running under the Interposer reports as its rich presence on to the server,
/// for the other players to see. Discord gets it from the DLL directly; this is the LANCommander side.
/// </summary>
public sealed class PresenceForwarder(PresenceClient presenceClient, ILogger<PresenceForwarder> logger)
{
    /// <summary>
    /// The least time between two reports for one game. A plugin may update its presence many times
    /// a second; only the latest within the interval is sent.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(2);

    /// <summary>Forwards the game's presence until the result is disposed, when the game has exited.</summary>
    public IDisposable Track(Guid gameId, string? gameTitle, InterposerService interposer)
        => new Tracker(gameId, gameTitle, interposer, (activity, _) => presenceClient.UpdateAsync(gameId, activity), MinimumInterval, logger);

    /// <summary>
    /// What of the presence a game reports can be shown outside Discord: no asset keys, and not the
    /// game's title, which the launcher sets as the default details and the card already shows.
    /// Null when nothing is left.
    /// </summary>
    public static PresenceActivity? ToActivity(RichPresence? presence, string? gameTitle)
    {
        if (presence == null)
            return null;

        var details = Text(presence.Details);

        if (details != null && gameTitle != null && string.Equals(details, gameTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            details = null;

        var activity = new PresenceActivity
        {
            Details = details,
            State = Text(presence.State),
            StartsOn = presence.TimestampStart > 0 ? DateTimeOffset.FromUnixTimeSeconds(presence.TimestampStart) : null,
            EndsOn = presence.TimestampEnd > 0 ? DateTimeOffset.FromUnixTimeSeconds(presence.TimestampEnd) : null,
            PartySize = Math.Max(0, presence.PartySize),
            PartyMax = Math.Max(0, presence.PartyMax),
            LargeImageUrl = Url(presence.LargeImage),
            LargeImageText = Text(presence.LargeImageText),
            SmallImageUrl = Url(presence.SmallImage),
            SmallImageText = Text(presence.SmallImageText),
        };

        return activity.HasContent ? activity : null;

        static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        static string? Url(string? value) =>
            Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? uri.ToString()
                : null;
    }

    /// <summary>One game's reports, held to <see cref="MinimumInterval"/> apart.</summary>
    internal sealed class Tracker : IDisposable
    {
        private readonly InterposerService? _interposer;
        private readonly string? _gameTitle;
        private readonly Guid _gameId;
        private readonly Func<PresenceActivity?, CancellationToken, Task> _send;
        private readonly TimeSpan _interval;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _lock = new();

        private PresenceActivity? _latest;
        private bool _scheduled;
        private DateTime _lastSent = DateTime.MinValue;

        internal Tracker(
            Guid gameId,
            string? gameTitle,
            InterposerService? interposer,
            Func<PresenceActivity?, CancellationToken, Task> send,
            TimeSpan interval,
            ILogger logger)
        {
            _gameId = gameId;
            _gameTitle = gameTitle;
            _interposer = interposer;
            _send = send;
            _interval = interval;
            _logger = logger;

            if (_interposer != null)
                _interposer.PresenceChanged += OnPresenceChanged;
        }

        private void OnPresenceChanged(object? sender, PresenceChangedEventArgs e) => Report(ToActivity(e.Presence, _gameTitle));

        /// <summary>Queues the game's latest activity: sent now, or once the interval since the last report has passed.</summary>
        internal void Report(PresenceActivity? activity)
        {
            lock (_lock)
            {
                _latest = activity;

                if (_scheduled || _cts.IsCancellationRequested)
                    return;

                _scheduled = true;
            }

            _ = SendAsync();
        }

        private async Task SendAsync()
        {
            try
            {
                TimeSpan wait;

                lock (_lock)
                    wait = _lastSent + _interval - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, _cts.Token);

                PresenceActivity? activity;

                lock (_lock)
                {
                    activity = _latest;
                    _scheduled = false;
                    _lastSent = DateTime.UtcNow;
                }

                await _send(activity, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // The game exited; the server ends its presence with the play session
            }
            catch (Exception ex)
            {
                lock (_lock)
                    _scheduled = false;

                _logger.LogDebug(ex, "Could not forward presence for game {GameId}", _gameId);
            }
        }

        public void Dispose()
        {
            if (_interposer != null)
                _interposer.PresenceChanged -= OnPresenceChanged;

            _cts.Cancel();
        }
    }
}
