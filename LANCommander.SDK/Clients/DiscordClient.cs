using System;
using System.Linq;
using DiscordRPC;
using DiscordRPC.Logging;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Clients
{
    public class DiscordClient : IDisposable
    {
        private const string DefaultApplicationId = "1272690976791461950";

        private readonly ILogger<DiscordClient> _logger;

        private DiscordRpcClient _client;

        public DiscordClient(ILogger<DiscordClient> logger)
        {
            _logger = logger;
        }

        public void UpdatePresence(Game game)
        {
            var discordAppId = game.ExternalIds?
                .FirstOrDefault(e => string.Equals(e.Provider, "Discord", StringComparison.OrdinalIgnoreCase))
                ?.ExternalId;

            UpdatePresence(game.Title, discordAppId);
        }

        public void UpdatePresence(string gameTitle, string discordAppId = null)
        {
            try
            {
                InitializeClient(discordAppId ?? DefaultApplicationId);

                var presence = new RichPresence()
                    .WithDetails(gameTitle)
                    .WithTimestamps(new Timestamps(DateTime.UtcNow));

                if (discordAppId != null)
                {
                    presence.WithAssets(new Assets()
                    {
                        LargeImageKey = "logo",
                        LargeImageText = gameTitle
                    });
                }

                _client?.SetPresence(presence);

                _logger.LogDebug("Discord presence updated for game {GameTitle}", gameTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Discord presence for game {GameTitle}", gameTitle);
            }
        }

        public void ClearPresence()
        {
            try
            {
                _client?.ClearPresence();
                _client?.Dispose();
                _client = null;

                _logger.LogDebug("Discord presence cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear Discord presence");
            }
        }

        private void InitializeClient(string applicationId)
        {
            if (_client != null)
            {
                if (_client.ApplicationID != applicationId)
                {
                    _client.ClearPresence();
                    _client.Dispose();
                    _client = null;
                }
                else
                {
                    return;
                }
            }

            _client = new DiscordRpcClient(applicationId)
            {
                Logger = new NullLogger()
            };

            _client.OnError += (sender, args) =>
            {
                _logger.LogWarning("Discord RPC error: {Message}", args.Message);
            };

            _client.Initialize();
        }

        public void Dispose()
        {
            _client?.ClearPresence();
            _client?.Dispose();
            _client = null;
        }

        private class NullLogger : DiscordRPC.Logging.ILogger
        {
            public DiscordRPC.Logging.LogLevel Level { get; set; } = DiscordRPC.Logging.LogLevel.None;

            public void Error(string message, params object[] args) { }
            public void Info(string message, params object[] args) { }
            public void Trace(string message, params object[] args) { }
            public void Warning(string message, params object[] args) { }
        }
    }
}
