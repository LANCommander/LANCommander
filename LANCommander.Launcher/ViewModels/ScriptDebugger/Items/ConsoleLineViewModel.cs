using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

/// <summary>One rendered line in the console panel.</summary>
public sealed class ConsoleLineViewModel
{
    private static readonly IBrush OutputBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
    private static readonly IBrush HostBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
    private static readonly IBrush ErrorBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47));
    private static readonly IBrush WarningBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xDC, 0xC0, 0x6A));
    private static readonly IBrush VerboseBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x6B, 0xC8, 0xD6));
    private static readonly IBrush DebugBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x9A, 0x8C, 0xD6));
    private static readonly IBrush InformationBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
    private static readonly IBrush PromptBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
    private static readonly IBrush EchoBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly IBrush SystemBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));

    public ConsoleLineViewModel(ConsoleOutputKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public ConsoleOutputKind Kind { get; }

    public string Text { get; }

    public IBrush Foreground => Kind switch
    {
        ConsoleOutputKind.Output => OutputBrush,
        ConsoleOutputKind.Host => HostBrush,
        ConsoleOutputKind.Error => ErrorBrush,
        ConsoleOutputKind.Warning => WarningBrush,
        ConsoleOutputKind.Verbose => VerboseBrush,
        ConsoleOutputKind.Debug => DebugBrush,
        ConsoleOutputKind.Information => InformationBrush,
        ConsoleOutputKind.Prompt => PromptBrush,
        ConsoleOutputKind.Echo => EchoBrush,
        ConsoleOutputKind.System => SystemBrush,
        _ => OutputBrush,
    };
}
