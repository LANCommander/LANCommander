using System;
using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Helpers;

/// <summary>A source of animation frames a control draws itself: a video, or an animated image.</summary>
internal interface IFrameAnimation : IDisposable
{
    /// <summary>The latest frame, or null until the first one is decoded.</summary>
    Bitmap? Frame { get; }

    /// <summary>Raised on the UI thread after <see cref="Frame"/> holds a new frame.</summary>
    Action? FrameReady { get; set; }

    /// <summary>Holds the current frame (true) or resumes playback (false).</summary>
    void SetPaused(bool paused);

    /// <summary>
    /// Playback speed relative to the source's own timing: 1 is normal, 0.5 half speed. Must be
    /// above zero; to stop, pause. Used to ease playback in and out rather than cutting it.
    /// </summary>
    void SetRate(double rate);
}
