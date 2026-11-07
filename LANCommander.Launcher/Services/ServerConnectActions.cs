using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.SDK.Enums;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Services;

/// <summary>Chooses what a game server's Connect button runs.</summary>
public static class ServerConnectActions
{
    /// <summary>
    /// The server's own primary action, else the game's first Join action pointed at the server
    /// (its host, port and id as the action's variables, which fill <c>{ServerHost}</c> and
    /// <c>{ServerPort}</c>). Null when the game has neither, so the server has no Connect button.
    /// </summary>
    /// <param name="actions">The game's actions as the launcher lists them, servers' actions included.</param>
    public static ManifestAction? Resolve(Guid serverId, string? host, int port, IEnumerable<ManifestAction> actions)
    {
        var list = actions as IList<ManifestAction> ?? actions.ToList();

        var own = list.FirstOrDefault(a =>
            a.Type == ActionType.Primary
            && ActionTypes.TryGetServerId(a, out var owner)
            && owner == serverId);

        if (own != null)
            return own;

        var join = list.FirstOrDefault(a => a.Type == ActionType.Join && !ActionTypes.IsServerAction(a));

        return join == null ? null : PointAt(join, serverId, host, port);
    }

    /// <summary>A copy of <paramref name="join"/> whose variables name the server; the game's action is left alone.</summary>
    private static ManifestAction PointAt(ManifestAction join, Guid serverId, string? host, int port)
    {
        var variables = join.Variables != null ? new Dictionary<string, string>(join.Variables) : new();

        if (!string.IsNullOrWhiteSpace(host))
            variables["ServerHost"] = host;

        if (port > 0)
            variables["ServerPort"] = port.ToString();

        variables[ActionTypes.ServerIdVariable] = serverId.ToString();

        return new ManifestAction
        {
            Id = join.Id,
            Name = join.Name,
            Arguments = join.Arguments,
            Path = join.Path,
            WorkingDirectory = join.WorkingDirectory,
            Type = join.Type,
            SortOrder = join.SortOrder,
            OptionOverrides = join.OptionOverrides,
            Platforms = join.Platforms,
            Variables = variables,
            ToolId = join.ToolId,
            CreatedBy = join.CreatedBy,
            CreatedOn = join.CreatedOn,
            UpdatedBy = join.UpdatedBy,
            UpdatedOn = join.UpdatedOn,
        };
    }
}
