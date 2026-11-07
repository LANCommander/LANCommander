using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.SDK.Enums;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Services.Protocol;

/// <summary>Chooses the action a <c>play</c> or <c>join</c> link runs and fills in the link's parameters.</summary>
public static class ProtocolActions
{
    /// <summary>
    /// The action a link runs. <c>play</c>: the action with <paramref name="actionId"/>, whatever its
    /// type, else the game's default (its primary action, else its first secondary one). <c>join</c>:
    /// the Join action with <paramref name="actionId"/>, else the game's first Join action. Null when
    /// there is no such action, rather than a different one than the link asked for.
    /// </summary>
    public static ManifestAction? Select(IEnumerable<ManifestAction> actions, ProtocolRequestKind kind, Guid? actionId)
    {
        var list = actions as IList<ManifestAction> ?? actions.ToList();

        if (kind == ProtocolRequestKind.JoinGame)
            list = list.Where(a => a.Type == ActionType.Join).ToList();

        if (actionId.HasValue)
            return list.FirstOrDefault(a => a.Id == actionId);

        // The list comes ordered as the launcher offers it (native before bridged, then sort order)
        return kind == ProtocolRequestKind.JoinGame
            ? list.FirstOrDefault()
            : ActionTypes.SelectDefault(list);
    }

    /// <summary>
    /// A copy of <paramref name="action"/> with each parameter written into the <c>{Name}</c>
    /// placeholders of its arguments. Only the arguments: the path and working directory decide what
    /// runs, which a link must not change, and a parameter that reached the action's variables
    /// would also replace built-in tokens such as <c>{InstallDir}</c> there. Placeholders the link
    /// doesn't fill are left for the action's own variables and the built-in tokens.
    /// </summary>
    public static ManifestAction ApplyParameters(ManifestAction action, IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return action;

        var arguments = action.Arguments ?? string.Empty;

        foreach (var (name, value) in parameters)
            arguments = arguments.Replace($"{{{name}}}", value, StringComparison.Ordinal);

        return new ManifestAction
        {
            Id = action.Id,
            Name = action.Name,
            Arguments = arguments,
            Path = action.Path,
            WorkingDirectory = action.WorkingDirectory,
            Type = action.Type,
            SortOrder = action.SortOrder,
            OptionOverrides = action.OptionOverrides,
            Platforms = action.Platforms,
            Variables = action.Variables != null ? new Dictionary<string, string>(action.Variables) : new(),
            ToolId = action.ToolId,
            CreatedBy = action.CreatedBy,
            CreatedOn = action.CreatedOn,
            UpdatedBy = action.UpdatedBy,
            UpdatedOn = action.UpdatedOn,
        };
    }

    /// <summary>The parameters with no placeholder in the action's arguments, which therefore do nothing.</summary>
    public static IReadOnlyList<string> UnusedParameters(ManifestAction action, IReadOnlyDictionary<string, string> parameters) =>
        parameters.Keys
            .Where(name => action.Arguments?.Contains($"{{{name}}}", StringComparison.Ordinal) != true)
            .ToList();
}
