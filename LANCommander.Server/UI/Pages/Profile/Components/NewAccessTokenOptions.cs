using LANCommander.Server.Services.Models;

namespace LANCommander.Server.UI.Pages.Profile.Components;

/// <summary>The plugins a new token can be limited to: those whose endpoints the user may call.</summary>
public sealed record NewAccessTokenOptions(IReadOnlyList<string> PluginRoutePrefixes);

public sealed record NewAccessTokenRequest(string Name, PersonalAccessTokenScopes Scopes, DateTime? ExpiresOn);
