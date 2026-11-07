using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Enums
{
    public static class ActionTypes
    {
        /// <summary>
        /// The type an action has after the old primary flag is set to <paramref name="isPrimary"/>:
        /// true makes it <see cref="ActionType.Primary"/>, false demotes a primary action to
        /// <see cref="ActionType.Secondary"/> and leaves any other type alone. Serializers may read the
        /// flag before or after the type, so false must not undo a Config or Join read first.
        /// </summary>
        public static ActionType FromPrimaryFlag(bool isPrimary, ActionType current) =>
            isPrimary
                ? ActionType.Primary
                : current == ActionType.Primary ? ActionType.Secondary : current;

        /// <summary>Where an action of <paramref name="type"/> sorts among a game's actions: play actions first.</summary>
        public static int Rank(ActionType type) => type switch
        {
            ActionType.Primary => 0,
            ActionType.Secondary => 1,
            ActionType.Config => 2,
            ActionType.Join => 3,
            _ => 4,
        };

        /// <summary>
        /// The variable the server stamps on a game server's own actions when it lists them with the
        /// game's, naming the game server they connect to.
        /// </summary>
        public const string ServerIdVariable = "ServerId";

        /// <summary>
        /// Whether <paramref name="action"/> belongs to one of the game's servers rather than to the game,
        /// and if so which. It's offered like any other action; a server's Connect button also runs it.
        /// </summary>
        public static bool TryGetServerId(Models.Manifest.Action action, out Guid serverId)
        {
            serverId = Guid.Empty;

            return action?.Variables != null
                && action.Variables.TryGetValue(ServerIdVariable, out var value)
                && Guid.TryParse(value, out serverId);
        }

        public static bool IsServerAction(Models.Manifest.Action action) => TryGetServerId(action, out _);

        /// <summary>
        /// The action to run when none was chosen: the game's first primary, else its first secondary.
        /// Never a Config or Join action, which don't play the game as it is.
        /// </summary>
        public static Models.Manifest.Action SelectDefault(IEnumerable<Models.Manifest.Action> actions)
        {
            var list = actions?.ToList() ?? new List<Models.Manifest.Action>();

            return list.FirstOrDefault(a => a.Type == ActionType.Primary)
                ?? list.FirstOrDefault(a => a.Type == ActionType.Secondary);
        }
    }
}
