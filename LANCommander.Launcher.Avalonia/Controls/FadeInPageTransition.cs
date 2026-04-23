using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// A page transition that immediately hides the old content and fades in the new content.
/// Avoids the z-index stacking artifacts that <see cref="CrossFade"/> causes when both
/// old and new content overlap during the transition.
/// </summary>
public class FadeInPageTransition : IPageTransition
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(200);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (from != null)
        {
            from.Opacity = 0;
            from.IsVisible = false;
        }

        if (to != null)
        {
            to.IsVisible = true;
            to.Opacity = 0;

            var animation = new Animation
            {
                Duration = Duration,
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };

            await animation.RunAsync(to, cancellationToken);
            to.Opacity = 1;
        }
    }
}
