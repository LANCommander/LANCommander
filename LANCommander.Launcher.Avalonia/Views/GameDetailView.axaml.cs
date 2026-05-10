using System.Collections.Generic;
using System.Linq;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using LANCommander.Launcher.Avalonia.Controls;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Avalonia.ViewModels.Components;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GameDetailView : UserControl
{
    public GameDetailView()
    {
        InitializeComponent();
    }

    private void MediaItem_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        if (panel.DataContext is not GameMediaItemViewModel tappedVm || tappedVm.IsSkeleton)
            return;

        if (DataContext is not GameDetailViewModel detailVm)
            return;

        // Build lightbox items from all loaded (non-skeleton) media
        var mediaItems = detailVm.MediaItems;
        var lightboxItems = new List<LightboxItem>();
        int tappedIndex = 0;

        for (int i = 0; i < mediaItems.Count; i++)
        {
            var m = mediaItems[i];
            if (m.IsSkeleton) continue;

            if (m == tappedVm)
                tappedIndex = lightboxItems.Count;

            lightboxItems.Add(new LightboxItem
            {
                Type = m.IsVideo ? LightboxItemType.Video : LightboxItemType.Image,
                Path = m.Path,
                ImageSource = m.ImageSource,
            });
        }

        // For video items, capture timestamp and pause the inline player
        long videoStartTimeMs = 0;
        InlineVideoPlayer? inlinePlayer = null;

        if (tappedVm.IsVideo)
        {
            // Find the InlineVideoPlayer inside the tapped panel
            var videoBorder = panel.Children
                .OfType<Decorator>()
                .FirstOrDefault(d => d.Child is InlineVideoPlayer);

            if (videoBorder?.Child is InlineVideoPlayer player)
            {
                inlinePlayer = player;
                videoStartTimeMs = player.CurrentTimeMs;
                player.Pause();
            }
        }

        var overlay = LightboxOverlay.ShowOverlay(lightboxItems, tappedIndex, videoStartTimeMs);

        // When the lightbox closes, resume the inline video player if one was paused
        if (inlinePlayer != null)
        {
            var capturedPlayer = inlinePlayer;
            var resumed = false;

            overlay.VideoClosed += (_, args) =>
            {
                // Sync timestamp back if we're still on the same video
                if (args.Index == tappedIndex)
                    capturedPlayer.ResumeAt(args.TimeMs);
                else
                    capturedPlayer.ResumeAt(0);
                resumed = true;
            };

            overlay.Closed += (_, _) =>
            {
                // If VideoClosed didn't fire (closed on a non-video item), resume anyway
                if (!resumed)
                    capturedPlayer.ResumeAt(0);
            };
        }
    }
}
