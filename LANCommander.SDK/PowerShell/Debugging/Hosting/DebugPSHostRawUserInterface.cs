#nullable enable
using System;
using System.Management.Automation;
using System.Management.Automation.Host;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// A virtual console surface. Only the size properties really matter: the formatting engine reads
/// <see cref="BufferSize"/>.Width to lay out Format-Table. The cursor and buffer-scraping members have
/// no meaning here.
/// </summary>
public sealed class DebugPSHostRawUserInterface : PSHostRawUserInterface
{
    private const int DefaultWidth = 200;

    public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.Gray;
    public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;

    public override Coordinates CursorPosition { get; set; } = new(0, 0);
    public override Coordinates WindowPosition { get; set; } = new(0, 0);
    public override int CursorSize { get; set; } = 25;
    public override Size BufferSize { get; set; } = new(DefaultWidth, 9999);
    public override Size WindowSize { get; set; } = new(DefaultWidth, 50);
    public override Size MaxWindowSize => new(DefaultWidth, 9999);
    public override Size MaxPhysicalWindowSize => new(DefaultWidth, 9999);
    public override string WindowTitle { get; set; } = "LANCommander Script Debugger";

    /// <summary>Always false: there is no key queue, and returning true would make scripts poll forever.</summary>
    public override bool KeyAvailable => false;

    public override KeyInfo ReadKey(ReadKeyOptions options) =>
        throw new PSNotImplementedException("Reading individual keys is not supported by the script debugger. Use Read-Host.");

    public override void FlushInputBuffer() { }

    public override BufferCell[,] GetBufferContents(Rectangle rectangle) =>
        throw new PSNotImplementedException("The script debugger has no screen buffer to read.");

    public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) { }

    public override void SetBufferContents(Rectangle rectangle, BufferCell fill) { }

    public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) { }
}
