using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

/// <summary>One row of the command pane. Immutable, like the call-stack rows.</summary>
public sealed class CommandItemViewModel
{
    private static readonly IBrush CmdletBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
    private static readonly IBrush FunctionBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
    private static readonly IBrush AliasBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    public CommandItemViewModel(CommandInfoSnapshot info) => Info = info;

    public CommandInfoSnapshot Info { get; }

    public string Name => Info.Name;

    public string ModuleDisplay => Info.ModuleDisplay;

    /// <summary>The row is too narrow for the module, so it goes in the tooltip with the version.</summary>
    public string Tooltip =>
        Info.ModuleDisplay + (Info.Version.Length == 0 ? string.Empty : " " + Info.Version);

    /// <summary>Short enough to sit in a narrow pane without a column header.</summary>
    public string TypeDisplay => Info.CommandType switch
    {
        "Cmdlet" => "cmdlet",
        "Function" => "function",
        "Alias" => "alias",
        var other => other.ToLowerInvariant(),
    };

    public IBrush NameBrush => Info.CommandType switch
    {
        "Function" => FunctionBrush,
        "Alias" => AliasBrush,
        _ => CmdletBrush,
    };
}
