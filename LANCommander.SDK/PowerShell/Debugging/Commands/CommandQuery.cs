#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Debugging.Commands;

/// <summary>
/// The only place in the engine that asks PowerShell what commands exist.
/// </summary>
/// <remarks>
/// <para>
/// It is written against an <see cref="Invoker"/> rather than a runspace so that the two callers
/// share one implementation and therefore produce identical rows: <see cref="CommandCatalog"/>
/// drives it with a plain pipeline against its own runspace, and the debugger work items drive it
/// with <c>Debugger.ProcessCommand</c> on the pipeline thread while stopped.
/// </para>
/// <para>
/// Everything here runs on the caller's thread and returns immutable snapshots.
/// </para>
/// </remarks>
internal static class CommandQuery
{
    /// <summary>Runs one <see cref="PSCommand"/> and collects its success stream.</summary>
    internal delegate void Invoker(PSCommand command, PSDataCollection<PSObject> output);

    /// <summary>
    /// Applications are excluded deliberately: every executable on PATH is a "command", which would
    /// bury the real cmdlets under several thousand .exe entries.
    /// </summary>
    private const CommandTypes Listed = CommandTypes.Cmdlet | CommandTypes.Function | CommandTypes.Alias;

    /// <summary>
    /// Parameters the engine adds to every advanced command. They say nothing about the command
    /// being inspected, and listing them would push the real parameters off the bottom of the pane.
    /// </summary>
    private static readonly HashSet<string> CommonParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Debug", "ErrorAction", "ErrorVariable", "InformationAction", "InformationVariable",
        "OutBuffer", "OutVariable", "PipelineVariable", "ProgressAction", "Verbose",
        "WarningAction", "WarningVariable",
    };

    /// <summary>
    /// Every command the session can resolve, including those exported by imported modules.
    /// </summary>
    /// <remarks>
    /// Only the four cheap properties are read. <c>ModuleName</c> and <c>Version</c> come out of the
    /// command-discovery cache; <c>Parameters</c> and <c>ParameterSets</c> do not, and touching them
    /// here would import every discoverable module on the machine.
    /// </remarks>
    internal static IReadOnlyList<CommandInfoSnapshot> List(Invoker invoke)
    {
        var output = new PSDataCollection<PSObject>();

        var command = new PSCommand()
            .AddCommand("Get-Command")
            .AddParameter("CommandType", Listed)
            // A broken manifest anywhere on PSModulePath writes an error and would otherwise abort
            // the whole enumeration over one unrelated module.
            .AddParameter("ErrorAction", ActionPreference.SilentlyContinue);

        invoke(command, output);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commands = new List<CommandInfoSnapshot>(output.Count);

        foreach (var item in output)
        {
            if (item?.BaseObject is not CommandInfo info)
                continue;

            string name;
            try
            {
                name = info.Name;
            }
            catch (Exception)
            {
                continue;
            }

            if (string.IsNullOrEmpty(name))
                continue;

            var module = SafeModuleName(info);

            // The same name is resolvable from more than one module version.
            if (!seen.Add(name + " " + module))
                continue;

            commands.Add(new CommandInfoSnapshot
            {
                Name = name,
                CommandType = info.CommandType.ToString(),
                ModuleName = module,
                Version = SafeVersion(info),
            });
        }

        commands.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return commands;
    }

    /// <summary>Synopsis, syntax and parameters for a single command.</summary>
    internal static CommandDetail? Describe(Invoker invoke, string name)
    {
        var resolved = new PSDataCollection<PSObject>();

        invoke(
            new PSCommand()
                .AddCommand("Get-Command")
                .AddParameter("Name", name)
                .AddParameter("ErrorAction", ActionPreference.SilentlyContinue),
            resolved);

        var info = resolved
            .Select(static o => o?.BaseObject as CommandInfo)
            .FirstOrDefault(static c => c is not null);

        if (info is null)
            return null;

        return new CommandDetail
        {
            Name = info.Name,
            ModuleName = SafeModuleName(info),
            Synopsis = ReadSynopsis(invoke, name),
            Syntax = ReadSyntax(invoke, name),
            Parameters = ReadParameters(info),
        };
    }

    private static string ReadSyntax(Invoker invoke, string name)
    {
        try
        {
            var output = new PSDataCollection<PSObject>();

            invoke(
                new PSCommand()
                    .AddCommand("Get-Command")
                    .AddParameter("Name", name)
                    .AddParameter("Syntax", true)
                    .AddParameter("ErrorAction", ActionPreference.SilentlyContinue),
                output);

            return string.Join(
                Environment.NewLine,
                output.Where(static o => o is not null)
                      .Select(static o => o.ToString().Trim())
                      .Where(static s => s.Length > 0));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ReadSynopsis(Invoker invoke, string name)
    {
        try
        {
            var output = new PSDataCollection<PSObject>();

            invoke(
                new PSCommand()
                    .AddCommand("Get-Help")
                    .AddParameter("Name", name)
                    .AddParameter("ErrorAction", ActionPreference.SilentlyContinue),
                output);

            var synopsis = output
                .Select(static o => o?.Properties["Synopsis"]?.Value as string)
                .FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s));

            synopsis = synopsis?.Trim() ?? string.Empty;

            // With no downloaded help, Get-Help synthesises a "synopsis" that is just the syntax
            // line, which the syntax box already shows.
            return synopsis.StartsWith(name, StringComparison.OrdinalIgnoreCase) ? string.Empty : synopsis;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<CommandParameterSnapshot> ReadParameters(CommandInfo info)
    {
        var parameters = new List<CommandParameterSnapshot>();

        try
        {
            foreach (var (parameterName, metadata) in info.Parameters)
            {
                if (CommonParameters.Contains(parameterName))
                    continue;

                var sets = metadata.ParameterSets.Values;

                var position = sets
                    .Select(static s => s.Position)
                    .Where(static p => p >= 0)
                    .Cast<int?>()
                    .FirstOrDefault();

                parameters.Add(new CommandParameterSnapshot
                {
                    Name = parameterName,
                    TypeName = metadata.ParameterType?.Name ?? string.Empty,
                    IsMandatory = sets.Any(static s => s.IsMandatory),
                    Position = position,
                    Aliases = string.Join(", ", metadata.Aliases),
                });
            }
        }
        catch (Exception)
        {
            // Reading Parameters imports the declaring module, which can fail for a module that is
            // discoverable but not loadable. Syntax and synopsis are still worth showing.
        }

        parameters.Sort(static (a, b) =>
        {
            if (a.IsMandatory != b.IsMandatory)
                return a.IsMandatory ? -1 : 1;

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return parameters;
    }

    private static string SafeModuleName(CommandInfo info)
    {
        try
        {
            return info.ModuleName ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string SafeVersion(CommandInfo info)
    {
        try
        {
            return info.Version?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
