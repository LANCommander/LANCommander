using System;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// Backs <c>$Host.PrivateData</c>. Scripts read these to pick colours; a few write them. Values are
/// advisory: the console maps <see cref="ConsoleOutputKind"/> to theme brushes.
/// </summary>
public sealed class HostPrivateData
{
    public ConsoleColor ErrorForegroundColor { get; set; } = ConsoleColor.Red;
    public ConsoleColor ErrorBackgroundColor { get; set; } = ConsoleColor.Black;
    public ConsoleColor WarningForegroundColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor WarningBackgroundColor { get; set; } = ConsoleColor.Black;
    public ConsoleColor DebugForegroundColor { get; set; } = ConsoleColor.Cyan;
    public ConsoleColor DebugBackgroundColor { get; set; } = ConsoleColor.Black;
    public ConsoleColor VerboseForegroundColor { get; set; } = ConsoleColor.Cyan;
    public ConsoleColor VerboseBackgroundColor { get; set; } = ConsoleColor.Black;
    public ConsoleColor ProgressForegroundColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor ProgressBackgroundColor { get; set; } = ConsoleColor.DarkCyan;
}
