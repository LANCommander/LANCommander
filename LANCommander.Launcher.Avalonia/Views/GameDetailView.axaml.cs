using System.Collections.Generic;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
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

    /// <summary>
    /// When an element receives focus via directional (gamepad) navigation,
    /// scroll it into view with some vertical padding so it isn't at the edge.
    /// </summary>
    private void OnScrollViewerGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Directional && e.Source is Control focused)
        {
            focused.BringIntoView(new Rect(0, -80, focused.Bounds.Width, focused.Bounds.Height + 160));
        }
    }

    /// <summary>
    /// Handle cross-column navigation for gamepad: Down from the bottom of the
    /// left column redirects to the right-column metadata badges, and Up from
    /// the metadata badges returns to the left column.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;

            if (focused != null)
            {
                switch (e.Key)
                {
                    case Key.Down when IsDescendantOf(focused, MediaSection):
                    {
                        var target = FindFirstFocusable(MetadataPanel);
                        if (target != null)
                        {
                            target.Focus(NavigationMethod.Directional);
                            e.Handled = true;
                            return;
                        }
                        break;
                    }

                    case Key.Up when IsDescendantOf(focused, MetadataPanel):
                    {
                        // Go back to the carousel if visible, otherwise the left column
                        var source = MediaSection.IsVisible ? MediaSection : (Visual)LeftContent;
                        var target = FindLastFocusable(source);
                        if (target != null)
                        {
                            target.Focus(NavigationMethod.Directional);
                            e.Handled = true;
                            return;
                        }
                        break;
                    }
                }
            }
        }

        base.OnKeyDown(e);
    }

    private static bool IsDescendantOf(Visual visual, Visual ancestor)
    {
        var current = visual;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current.GetVisualParent();
        }
        return false;
    }

    private static InputElement? FindFirstFocusable(Visual root)
    {
        return root.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(el => el.Focusable && el.IsEffectivelyVisible && el.IsEffectivelyEnabled);
    }

    private static InputElement? FindLastFocusable(Visual root)
    {
        return root.GetVisualDescendants()
            .OfType<InputElement>()
            .LastOrDefault(el => el.Focusable && el.IsEffectivelyVisible && el.IsEffectivelyEnabled);
    }

    private void MediaItem_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            OpenMediaLightbox(sender as Panel);
            e.Handled = true;
        }
    }

    private void MediaItem_Tapped(object? sender, TappedEventArgs e)
    {
        OpenMediaLightbox(sender as Panel);
    }

    private void OpenMediaLightbox(Panel? panel)
    {
        if (panel == null)
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
