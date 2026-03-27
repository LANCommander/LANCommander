using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LANCommander.Launcher.Avalonia.Tests.Helpers;

/// <summary>
/// Pixel-level image comparison for visual regression detection.
/// </summary>
public static class VisualComparer
{
    /// <summary>
    /// Per-channel tolerance (0–255). Pixels within this delta on every channel
    /// are considered identical. Handles minor anti-aliasing and font-hint variation.
    /// </summary>
    public const int DefaultTolerancePerChannel = 10;

    /// <summary>
    /// Maximum proportion of pixels (0–100) that may differ before the comparison fails.
    /// 1% allows for small, unavoidable rendering differences across platforms.
    /// </summary>
    public const double DefaultMaxMismatchPercent = 1.0;

    /// <summary>
    /// Compares two screenshots pixel by pixel.
    /// </summary>
    /// <param name="actualPath">Path to the screenshot captured in this test run.</param>
    /// <param name="baselinePath">Path to the committed baseline image.</param>
    /// <param name="diffOutputPath">Where to write a diff-highlight image when differences are found.</param>
    /// <param name="tolerancePerChannel">Per-channel delta considered "identical".</param>
    /// <param name="maxMismatchPercent">Maximum differing-pixel percentage before failure.</param>
    /// <returns>A <see cref="ComparisonResult"/> describing what was found.</returns>
    public static ComparisonResult Compare(
        string actualPath,
        string baselinePath,
        string diffOutputPath,
        int tolerancePerChannel = DefaultTolerancePerChannel,
        double maxMismatchPercent = DefaultMaxMismatchPercent)
    {
        if (!File.Exists(baselinePath))
        {
            return ComparisonResult.NoBaseline(baselinePath);
        }

        using var actual = Image.Load<Rgba32>(actualPath);
        using var baseline = Image.Load<Rgba32>(baselinePath);

        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return ComparisonResult.SizeMismatch(
                actual.Width, actual.Height,
                baseline.Width, baseline.Height);
        }

        int mismatchCount = 0;
        int totalPixels = actual.Width * actual.Height;

        // Build a diff image: mismatched pixels → vivid red, matched → dimmed grayscale.
        using var diff = new Image<Rgba32>(actual.Width, actual.Height);

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var a = actual[x, y];
                var b = baseline[x, y];

                bool differs =
                    Math.Abs(a.R - b.R) > tolerancePerChannel ||
                    Math.Abs(a.G - b.G) > tolerancePerChannel ||
                    Math.Abs(a.B - b.B) > tolerancePerChannel;

                if (differs)
                {
                    mismatchCount++;
                    diff[x, y] = new Rgba32(255, 0, 80, 255); // red highlight
                }
                else
                {
                    // Dim matched pixels so regressions stand out visually.
                    byte luma = (byte)((a.R * 299 + a.G * 587 + a.B * 114) / 1000 / 3);
                    diff[x, y] = new Rgba32(luma, luma, luma, 255);
                }
            }
        }

        double mismatchPercent = (double)mismatchCount / totalPixels * 100.0;

        if (mismatchPercent > 0)
        {
            diff.SaveAsPng(diffOutputPath);
        }

        return new ComparisonResult
        {
            BaselineExists     = true,
            SizesMatch         = true,
            MismatchPixelCount = mismatchCount,
            TotalPixelCount    = totalPixels,
            MismatchPercent    = mismatchPercent,
            IsDifferent        = mismatchPercent > maxMismatchPercent,
            DiffImagePath      = mismatchPercent > 0 ? diffOutputPath : null,
        };
    }
}

public class ComparisonResult
{
    public bool BaselineExists     { get; init; }
    public bool SizesMatch         { get; init; }
    public int  MismatchPixelCount { get; init; }
    public int  TotalPixelCount    { get; init; }
    public double MismatchPercent  { get; init; }
    public bool IsDifferent        { get; init; }
    public string? DiffImagePath   { get; init; }
    public string? FailureReason   { get; init; }

    public bool Passed => BaselineExists && SizesMatch && !IsDifferent;

    /// <summary>Human-readable summary for xUnit failure messages.</summary>
    public string Summary =>
        !BaselineExists
            ? $"No baseline found: {FailureReason}. The screenshot captured by this run has been saved; commit it as the baseline."
        : !SizesMatch
            ? $"Image size changed: {FailureReason}"
        : IsDifferent
            ? $"Visual regression detected: {MismatchPercent:F2}% of pixels differ ({MismatchPixelCount:N0} / {TotalPixelCount:N0}). Diff image: {DiffImagePath}"
        : $"Pass — {MismatchPercent:F2}% pixel mismatch (within tolerance).";

    public static ComparisonResult NoBaseline(string baselinePath) => new()
    {
        BaselineExists = false,
        FailureReason  = baselinePath,
        IsDifferent    = true,
    };

    public static ComparisonResult SizeMismatch(int aw, int ah, int bw, int bh) => new()
    {
        BaselineExists = true,
        SizesMatch     = false,
        FailureReason  = $"actual={aw}×{ah}, baseline={bw}×{bh}",
        IsDifferent    = true,
    };
}
