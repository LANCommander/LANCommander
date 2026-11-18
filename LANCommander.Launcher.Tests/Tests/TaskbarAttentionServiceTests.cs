using System;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Services;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>The unread badge drawn over the launcher's taskbar button</summary>
public class TaskbarAttentionServiceTests
{
    [AvaloniaTheory]
    [InlineData(1, 1.0)]
    [InlineData(12, 1.5)]
    [InlineData(250, 2.0)]
    public void Badge_IsAFilledCircleSizedToTheDisplay(int count, double scaling)
    {
        var (pixels, size) = TaskbarAttentionService.RenderBadge(count, scaling);

        Assert.Equal((int)Math.Round(16 * scaling), size);

        // Transparent in the corners, opaque red at the left edge's middle (clear of the digits)
        Assert.Equal(0, Pixel(pixels, size, 0, 0).A);

        var edge = Pixel(pixels, size, 2, size / 2);

        Assert.Equal(255, edge.A);
        Assert.True(edge.R > 200 && edge.G < 100 && edge.B < 100, $"Expected red, got {edge}");
    }

    [AvaloniaFact]
    public void Badge_BecomesAWindowsIcon()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var (pixels, size) = TaskbarAttentionService.RenderBadge(3, 1.0);
        var icon = TaskbarAttentionService.CreateIcon(pixels, size);

        try
        {
            Assert.NotEqual(IntPtr.Zero, icon);
        }
        finally
        {
            TaskbarAttentionService.DestroyIcon(icon);
        }
    }

    static (byte B, byte G, byte R, byte A) Pixel(byte[] pixels, int size, int x, int y)
    {
        var i = (y * size + x) * 4;

        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }
}
