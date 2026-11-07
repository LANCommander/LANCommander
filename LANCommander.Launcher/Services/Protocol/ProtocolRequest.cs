using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LANCommander.Launcher.Services.Protocol;

public enum ProtocolRequestKind
{
    /// <summary><c>lancommander://game/{id}</c>: open the game's page.</summary>
    ViewGame,

    /// <summary><c>lancommander://game/{id}/play</c>: open the game's page and start it.</summary>
    PlayGame,

    /// <summary><c>lancommander://game/{id}/join</c>: open the game's page and run its Join action.</summary>
    JoinGame,
}

/// <summary>
/// A <c>lancommander://</c> link the launcher was asked to open.
/// <list type="bullet">
/// <item><c>lancommander://game/{gameId}</c></item>
/// <item><c>lancommander://game/{gameId}/play</c></item>
/// <item><c>lancommander://game/{gameId}/play?action={actionId}&amp;Name=Value</c></item>
/// <item><c>lancommander://game/{gameId}/join?ServerHost=10.0.0.5&amp;ServerPort=27015</c></item>
/// </list>
/// <c>action</c> picks the action to run: for <c>play</c> any of the game's actions, otherwise its
/// primary; for <c>join</c> one of its Join actions, otherwise the first. Every other query parameter
/// fills the matching <c>{Name}</c> placeholder in that action's arguments.
/// </summary>
public sealed partial record ProtocolRequest(
    ProtocolRequestKind Kind,
    Guid GameId,
    Guid? ActionId,
    IReadOnlyDictionary<string, string> Parameters)
{
    public const string Scheme = "lancommander";

    private const string ActionParameter = "action";

    /// <summary>Whether <paramref name="value"/> is meant for this handler, whether or not it is well formed.</summary>
    public static bool IsProtocolUrl(string? value) =>
        value != null && value.StartsWith($"{Scheme}:", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string? value, out ProtocolRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (!IsProtocolUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            error = "The link isn't a valid LANCommander link.";
            return false;
        }

        if (!uri.Host.Equals("game", StringComparison.OrdinalIgnoreCase))
        {
            error = $"This version of the launcher can't open \"{uri.Host}\" links.";
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !Guid.TryParse(Uri.UnescapeDataString(segments[0]), out var gameId))
        {
            error = "The link doesn't name a game.";
            return false;
        }

        ProtocolRequestKind kind;

        if (segments.Length == 1)
            kind = ProtocolRequestKind.ViewGame;
        else if (segments.Length == 2 && segments[1].Equals("play", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolRequestKind.PlayGame;
        else if (segments.Length == 2 && segments[1].Equals("join", StringComparison.OrdinalIgnoreCase))
            kind = ProtocolRequestKind.JoinGame;
        else
        {
            error = $"This version of the launcher can't open \"{string.Join('/', segments.Skip(1))}\" game links.";
            return false;
        }

        Guid? actionId = null;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        // A link that only opens the game's page has nothing to apply its query to
        if (kind != ProtocolRequestKind.ViewGame)
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var name = Decode(separator < 0 ? pair : pair[..separator]);
                var parameterValue = separator < 0 ? string.Empty : Decode(pair[(separator + 1)..]);

                if (name.Equals(ActionParameter, StringComparison.OrdinalIgnoreCase))
                {
                    // Running the default action in place of the one asked for would be the wrong game mode
                    if (!Guid.TryParse(parameterValue, out var parsedActionId))
                    {
                        error = "The link's action isn't a valid action id.";
                        return false;
                    }

                    actionId = parsedActionId;
                    continue;
                }

                if (!ParameterName().IsMatch(name))
                {
                    error = $"\"{name}\" isn't a valid parameter name. Names use letters, digits and underscores.";
                    return false;
                }

                // A quote or a line break would end the argument the value was meant to fill and start new ones
                if (parameterValue.Any(c => c == '"' || char.IsControl(c)))
                {
                    error = $"The value of \"{name}\" contains characters a link can't pass to a game.";
                    return false;
                }

                parameters[name] = parameterValue;
            }
        }

        request = new ProtocolRequest(kind, gameId, actionId, parameters);
        return true;
    }

    /// <summary>Query values follow form encoding, where a space may be written as <c>+</c>.</summary>
    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ParameterName();
}
