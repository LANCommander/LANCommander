using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Allows the Win32 render backend to be chosen at runtime via environment variables.
/// </summary>
public static class RenderingOptions
{
    public const string RenderingModeVariable = "LANCOMMANDER_RENDERING_MODE";
    public const string CompositionModeVariable = "LANCOMMANDER_COMPOSITION_MODE";

    /// <summary>
    /// Describes the override that was applied, for logging once the logger exists.
    /// Null when no override was requested and Avalonia's defaults are in effect.
    /// </summary>
    public static string? AppliedOverride { get; private set; }

    private static readonly Dictionary<string, Win32RenderingMode> RenderingModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["software"] = Win32RenderingMode.Software,
            ["angle"] = Win32RenderingMode.AngleEgl,
            ["angleegl"] = Win32RenderingMode.AngleEgl,
            ["wgl"] = Win32RenderingMode.Wgl,
            ["opengl"] = Win32RenderingMode.Wgl,
            ["vulkan"] = Win32RenderingMode.Vulkan,
        };

    private static readonly Dictionary<string, Win32CompositionMode> CompositionModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["winui"] = Win32CompositionMode.WinUIComposition,
            ["winuicomposition"] = Win32CompositionMode.WinUIComposition,
            ["directcomposition"] = Win32CompositionMode.DirectComposition,
            ["directcomp"] = Win32CompositionMode.DirectComposition,
            ["lowlatency"] = Win32CompositionMode.LowLatencyDxgiSwapChain,
            ["lowlatencydxgiswapchain"] = Win32CompositionMode.LowLatencyDxgiSwapChain,
            ["redirection"] = Win32CompositionMode.RedirectionSurface,
            ["redirectionsurface"] = Win32CompositionMode.RedirectionSurface,
        };

    /// <summary>
    /// Applies the environment-driven render backend override, if one was requested.
    /// Unset or unrecognized values leave Avalonia's platform detection untouched.
    /// </summary>
    public static AppBuilder WithRenderingOverrides(this AppBuilder builder)
    {
        var renderingModes = Parse(Environment.GetEnvironmentVariable(RenderingModeVariable), RenderingModes);
        var compositionModes = Parse(Environment.GetEnvironmentVariable(CompositionModeVariable), CompositionModes);

        if (renderingModes.Count == 0 && compositionModes.Count == 0)
            return builder;

        // Anything left unset keeps the Avalonia default, so overriding one axis
        // (say, rendering only) doesn't silently reset the other.
        var options = new Win32PlatformOptions();

        if (renderingModes.Count > 0)
            options.RenderingMode = renderingModes;

        if (compositionModes.Count > 0)
            options.CompositionMode = compositionModes;

        AppliedOverride =
            $"rendering=[{string.Join(", ", options.RenderingMode)}], " +
            $"composition=[{string.Join(", ", options.CompositionMode)}]";

        return builder.With(options);
    }

    /// <summary>
    /// Parses a comma-separated preference list. Order is preserved because Avalonia treats
    /// it as a fallback chain, trying each in turn until one initializes.
    /// </summary>
    private static IReadOnlyList<TMode> Parse<TMode>(string? value, Dictionary<string, TMode> lookup)
        where TMode : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => lookup.TryGetValue(entry, out var mode) ? mode : (TMode?)null)
            .Where(mode => mode.HasValue)
            .Select(mode => mode!.Value)
            .Distinct()
            .ToList();
    }
}
