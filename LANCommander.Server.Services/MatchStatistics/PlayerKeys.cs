namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Player keys are a kind and a value: <c>steam:STEAM_0:1:123</c>, <c>name:Pat</c>. A key without a
/// kind is taken as a name.
/// </summary>
public static class PlayerKeys
{
    public const string NameKind = "name";

    public const int MaxKindLength = 32;
    public const int MaxValueLength = 158;
    public const int MaxLength = 191;

    /// <summary>
    /// Splits a key into its lowercased kind and its value. Names are lowercased too, since games
    /// report them in whatever case the player typed; other ids are kept exactly.
    /// </summary>
    public static (string Kind, string Value) Normalize(string playerKey)
    {
        var key = playerKey.Trim();
        var separator = key.IndexOf(':');

        string kind, value;

        if (separator <= 0)
        {
            kind = NameKind;
            value = key;
        }
        else
        {
            kind = key[..separator].Trim().ToLowerInvariant();
            value = key[(separator + 1)..].Trim();
        }

        if (kind == NameKind)
            value = value.ToLowerInvariant();

        return (kind, value);
    }

    public static string Format(string kind, string value) => $"{kind}:{value}";

    /// <summary>The name in a <c>name:</c> key, as a participant's display name; null for other kinds.</summary>
    public static string? DisplayNameOf(string playerKey)
    {
        var key = playerKey.Trim();

        if (key.StartsWith(NameKind + ":", StringComparison.OrdinalIgnoreCase))
            return key[(NameKind.Length + 1)..].Trim();

        return key.Contains(':') ? null : key;
    }

    public static bool IsValid(string? playerKey, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(playerKey))
        {
            error = "a player key is required";
            return false;
        }

        if (playerKey.Length > MaxLength)
        {
            error = $"player keys are at most {MaxLength} characters";
            return false;
        }

        var (kind, value) = Normalize(playerKey);

        if (kind.Length > MaxKindLength || value.Length > MaxValueLength || value.Length == 0)
        {
            error = $"a player key's kind is at most {MaxKindLength} characters and its value 1 to {MaxValueLength}";
            return false;
        }

        return true;
    }
}
