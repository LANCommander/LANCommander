using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class PersonalAccessTokenScopesTests
{
    [Theory]
    [InlineData("/api/Games")]
    [InlineData("/api/Plugins/Mcp/Tools")]
    [InlineData("/hubs/chat")]
    public void FullApiAllowsEveryPath(string path) =>
        PersonalAccessTokenScopes.FullApi.Allows(new PathString(path)).ShouldBeTrue();

    [Theory]
    [InlineData("/api/Plugins/Mcp", true)]
    [InlineData("/api/Plugins/Mcp/Tools/List", true)]
    [InlineData("/API/PLUGINS/mcp/tools", true)]
    [InlineData("/api/Plugins/McpAdmin", false)]
    [InlineData("/api/Plugins/Other/Ping", false)]
    [InlineData("/api/Plugins", false)]
    [InlineData("/api/Games", false)]
    [InlineData("/", false)]
    public void APluginScopeOnlyAllowsThatPluginsEndpoints(string path, bool allowed) =>
        PersonalAccessTokenScopes.ForPlugins(["Mcp"]).Allows(new PathString(path)).ShouldBe(allowed);

    [Fact]
    public void ScopesRoundTripThroughTheirStoredForm()
    {
        var scopes = PersonalAccessTokenScopes.Parse(PersonalAccessTokenScopes.ForPlugins(["Mcp", "Relay"]).ToString());

        scopes.HasFullApi.ShouldBeFalse();
        scopes.Plugins.ShouldBe(["Mcp", "Relay"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("api:write")]
    [InlineData("plugin:")]
    public void UnknownOrEmptyScopesGrantNothing(string? stored)
    {
        var scopes = PersonalAccessTokenScopes.Parse(stored);

        scopes.Allows(new PathString("/api/Games")).ShouldBeFalse();
        scopes.Allows(new PathString("/api/Plugins/Mcp")).ShouldBeFalse();
    }

    [Fact]
    public void APluginScopeNeedsAPlugin() =>
        Should.Throw<ArgumentException>(() => PersonalAccessTokenScopes.ForPlugins([" "]));
}
