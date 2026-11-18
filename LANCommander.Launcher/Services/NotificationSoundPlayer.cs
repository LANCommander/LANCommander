using System;
using System.IO;
using Avalonia.Platform;
using LANCommander.Launcher.Helpers;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Plays the launcher's own notification sounds. Desktop notifications can't carry them:
/// Windows toasts only take custom audio from packaged apps, and Linux and macOS take none,
/// so they're sent silent and the sound is played here, through the LibVLC the launcher
/// already ships for trailers.
/// </summary>
public sealed class NotificationSoundPlayer(ILogger<NotificationSoundPlayer> logger) : IDisposable
{
    const string ChatMessageAsset = "avares://LANCommander.Launcher/Assets/Sounds/chat-message.mp3";

    // Close together, a burst of messages plays one chime rather than a pile of them
    static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(1);

    readonly object _lock = new();
    MediaPlayer? _player;
    string? _chatMessagePath;
    DateTime _lastPlayed;

    public void PlayChatMessage()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _lastPlayed < MinimumGap)
                return;

            _lastPlayed = DateTime.UtcNow;

            try
            {
                _chatMessagePath ??= Extract(ChatMessageAsset, "chat-message.mp3");

                var libVlc = VideoFrameRenderer.SharedLibVLC;

                _player ??= new MediaPlayer(libVlc);

                using var media = new Media(libVlc, _chatMessagePath, FromType.FromPath);

                _player.Play(media);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not play the chat message sound");
            }
        }
    }

    /// <summary>LibVLC reads files, not Avalonia resources, so the sound is written out once</summary>
    static string Extract(string asset, string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "LANCommander", "Sounds", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var source = AssetLoader.Open(new Uri(asset));
        using var target = File.Create(path);

        source.CopyTo(target);

        return path;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _player?.Dispose();
            _player = null;
        }
    }
}
