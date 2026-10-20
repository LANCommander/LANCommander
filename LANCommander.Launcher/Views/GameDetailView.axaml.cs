using System;
using System.Collections.Generic;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Views;

public partial class GameDetailView : UserControl
{
    private bool _pluginTabsAdded;

    // Logos arrive in every shape, from near-square badges to long wordmarks. Giving each the same
    // area keeps their visual weight similar; the caps stop extreme ratios from running off.
    private const double LogoArea = 52_000;
    private const double LogoMaxWidth = 560;
    private const double LogoMaxHeight = 200;

    /// <summary>Each tab's header and content. Only the selected content is attached to <c>TabHost</c>.</summary>
    private readonly List<(Button Header, Control Content)> _tabs = new();

    public GameDetailView()
    {
        InitializeComponent();

        _tabs.Add((OverviewTabButton, OverviewContent));
        _tabs.Add((MediaTabButton, MediaContent));
        SelectTab(OverviewTabButton);

        LogoImage.PropertyChanged += (_, e) =>
        {
            if (e.Property == Image.SourceProperty)
                SizeLogo();
        };

        DataContextChanged += (_, _) =>
        {
            AppendPluginTabs();
            SelectTab(OverviewTabButton);
        };
    }

    /// <summary>
    /// Sets the logo's size limits from its aspect ratio: equal area, then capped by width and height.
    /// They're maximums rather than a fixed size, so a narrow window still shrinks the logo in proportion.
    /// </summary>
    private void SizeLogo()
    {
        var size = LogoImage.Source?.Size ?? default;

        if (size.Width <= 0 || size.Height <= 0)
        {
            LogoImage.MaxWidth = LogoMaxWidth;
            LogoImage.MaxHeight = LogoMaxHeight;
            return;
        }

        var aspect = size.Width / size.Height;
        var height = Math.Sqrt(LogoArea / aspect);
        var width = height * aspect;

        if (width > LogoMaxWidth)
        {
            width = LogoMaxWidth;
            height = width / aspect;
        }

        if (height > LogoMaxHeight)
        {
            height = LogoMaxHeight;
            width = height * aspect;
        }

        LogoImage.MaxWidth = width;
        LogoImage.MaxHeight = height;
    }

    private void TabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button header)
            SelectTab(header);
    }

    /// <summary>
    /// Shows one tab. Unselected content is detached rather than hidden, so inline videos in the
    /// Media tab stop instead of playing twice alongside the overview strip.
    /// </summary>
    private void SelectTab(Button header)
    {
        foreach (var (tabHeader, content) in _tabs)
        {
            var selected = tabHeader == header;

            tabHeader.Classes.Set("active", selected);

            if (selected && content.Parent == null)
                TabHost.Children.Add(content);
            else if (!selected && content.Parent == TabHost)
                TabHost.Children.Remove(content);
        }
    }

    /// <summary>
    /// Adds a tab per plugin detail extension (<see cref="Plugins.Extensions.IGameDetailTabExtension"/>)
    /// once a game is bound, after the built-in tabs. A failing extension is skipped so the built-in
    /// tabs still render.
    /// </summary>
    private void AppendPluginTabs()
    {
        if (_pluginTabsAdded || DataContext is not GameDetailViewModel detailVm)
            return;

        var extensions = App.Services?
            .GetServices<Plugins.Extensions.IGameDetailTabExtension>()
            .OrderBy(c => c.Order)
            .ToList();

        if (extensions == null || extensions.Count == 0)
            return;

        _pluginTabsAdded = true;

        foreach (var extension in extensions)
        {
            Control content;

            try
            {
                content = extension.BuildContent(detailVm.Id);
            }
            catch
            {
                continue;
            }

            var header = new Button { Content = extension.Header };
            header.Classes.Add("DetailTab");
            header.Click += TabButton_Click;

            TabStrip.Children.Add(header);
            _tabs.Add((header, content));
        }
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
                        // Go back to the carousel if it's on screen (Overview tab), otherwise the left column
                        var source = MediaSection.IsVisible && MediaSection.GetVisualRoot() != null
                            ? MediaSection
                            : (Visual)LeftContent;
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
                Title = m.Name,
                // The carousel bitmap is a downscaled thumbnail; let the lightbox
                // load the full-resolution image from Path for fullscreen display.
                ImageSource = m.IsVideo ? m.ImageSource : null,
            });
        }

        // For video items, capture timestamp and pause the inline player
        long videoStartTimeMs = 0;
        InlineVideoPlayer? inlinePlayer = null;

        if (tappedVm.IsVideo)
        {
            // Find the InlineVideoPlayer inside the tapped panel. It lives inside a
            // ContentControl's lazily-realized template, so search descendants.
            var player = panel.GetVisualDescendants().OfType<InlineVideoPlayer>().FirstOrDefault();

            if (player != null)
            {
                inlinePlayer = player;
                videoStartTimeMs = player.CurrentTimeMs;
                player.Pause();
            }
        }

        var overlay = LightboxOverlay.ShowOverlay(lightboxItems, tappedIndex, videoStartTimeMs, detailVm.Title);

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
