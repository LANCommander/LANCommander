using Microsoft.AspNetCore.Http;

namespace LANCommander.Server.Services.Models;

/// <summary>
/// What a personal access token may reach. A token never grants more than its owner's account: the
/// scopes only narrow which requests the token is accepted on.
/// </summary>
/// <remarks>
/// Scopes are stored space-separated, OAuth style, so new ones can be added without a schema change:
/// <list type="bullet">
/// <item><c>api</c>: every request the owner could make with a bearer token.</item>
/// <item><c>plugin:{RoutePrefix}</c>: only the endpoints a plugin maps under
/// <c>/api/Plugins/{RoutePrefix}</c>.</item>
/// </list>
/// Unknown scopes are kept but grant nothing, so a token issued by a newer server can't widen on an
/// older one.
/// </remarks>
public sealed class PersonalAccessTokenScopes
{
    public const string Api = "api";
    public const string PluginPrefix = "plugin:";

    private const string PluginRoot = "/api/Plugins";

    private readonly IReadOnlyList<string> _scopes;

    private PersonalAccessTokenScopes(IEnumerable<string> scopes)
    {
        _scopes = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static PersonalAccessTokenScopes FullApi { get; } = new([Api]);

    public static PersonalAccessTokenScopes ForPlugins(IEnumerable<string> routePrefixes)
    {
        var prefixes = routePrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => PluginPrefix + p.Trim())
            .ToArray();

        if (prefixes.Length == 0)
            throw new ArgumentException("At least one plugin is required.", nameof(routePrefixes));

        if (prefixes.Any(p => p.Any(char.IsWhiteSpace)))
            throw new ArgumentException("A plugin route prefix can't contain whitespace.", nameof(routePrefixes));

        return new PersonalAccessTokenScopes(prefixes);
    }

    public static PersonalAccessTokenScopes Parse(string? value) =>
        new((value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public bool HasFullApi => _scopes.Contains(Api, StringComparer.OrdinalIgnoreCase);

    /// <summary>The route prefixes of the plugins the token is limited to.</summary>
    public IReadOnlyList<string> Plugins => _scopes
        .Where(s => s.StartsWith(PluginPrefix, StringComparison.OrdinalIgnoreCase))
        .Select(s => s[PluginPrefix.Length..])
        .Where(p => p.Length > 0)
        .ToArray();

    /// <summary>Whether a request for <paramref name="path"/> may be authenticated with the token.</summary>
    public bool Allows(PathString path)
    {
        if (HasFullApi)
            return true;

        // StartsWithSegments matches whole segments, so plugin:Mcp doesn't reach /api/Plugins/McpAdmin
        return Plugins.Any(prefix => path.StartsWithSegments($"{PluginRoot}/{prefix}", StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => string.Join(' ', _scopes);
}
