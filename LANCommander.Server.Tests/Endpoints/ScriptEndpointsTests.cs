using System.Net;
using System.Net.Http.Json;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// <c>POST /api/Scripts/{id}/Contents</c> lets an administrator publish script edits made in the
/// launcher's script debugger.
/// </summary>
/// <remarks>
/// Handlers are invoked directly, as in <see cref="ToolsEndpointsTests"/>, to stay off the fixture's
/// shared authentication token. Only the authorization check goes over HTTP, with a fresh client.
/// </remarks>
[Collection("Application")]
public class ScriptEndpointsTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private async Task<(Tool Tool, Script Script)> SeedAsync(string contents, bool requiresAdmin = false)
    {
        var tool = await GetService<ToolService>().AddAsync(new Tool { Name = $"Test Tool {Guid.NewGuid()}" });

        var script = await GetService<ScriptService>().AddAsync(new Script
        {
            Name = "Install",
            Type = ScriptType.Install,
            Contents = contents,
            RequiresAdmin = requiresAdmin,
            ToolId = tool.Id,
        });

        return (tool, script);
    }

    private Task<IResult> UpdateAsync(Guid id, string contents, string? baseContentsHash = null) =>
        ScriptEndpoints.UpdateContentsAsync(
            id,
            new SDK.Models.UpdateScriptContentsRequest { Contents = contents, BaseContentsHash = baseContentsHash },
            GetService<ScriptService>(),
            GetService<SdkMapper>());

    [Fact]
    public async Task UpdateContents_ReplacesTheContents_AndLeavesEverythingElse()
    {
        var (_, script) = await SeedAsync("# before", requiresAdmin: true);

        var result = await UpdateAsync(script.Id, "# after");

        var updated = result.ShouldBeOfType<Ok<SDK.Models.Script>>().Value!;
        updated.Contents.ShouldBe("# after");

        var stored = await GetService<ScriptService>().GetAsync(script.Id);
        stored.Contents.ShouldBe("# after");
        stored.Name.ShouldBe("Install");
        stored.Type.ShouldBe(ScriptType.Install);
        stored.RequiresAdmin.ShouldBeTrue();
        stored.ToolId.ShouldBe(script.ToolId);
    }

    /// <summary>The per-owner script lists are cached; an upload has to be visible to the next launcher that asks.</summary>
    [Fact]
    public async Task UpdateContents_InvalidatesTheCachedScriptList()
    {
        var (tool, script) = await SeedAsync("# before");

        async Task<string?> ReadAsync()
        {
            var result = await ToolsEndpoints.GetScriptsByIdAsync(
                GetService<ScriptService>(), GetService<IFusionCache>(), GetService<SdkMapper>(), tool.Id);

            return result.ShouldBeOfType<Ok<List<SDK.Models.Script>>>().Value!.Single().Contents;
        }

        (await ReadAsync()).ShouldBe("# before");

        await UpdateAsync(script.Id, "# after");

        (await ReadAsync()).ShouldBe("# after");
    }

    [Fact]
    public async Task UpdateContents_WithTheCurrentHash_Succeeds()
    {
        var (_, script) = await SeedAsync("# base");

        var result = await UpdateAsync(script.Id, "# edited", ScriptHelper.HashContents("# base"));

        result.ShouldBeOfType<Ok<SDK.Models.Script>>();
    }

    [Fact]
    public async Task UpdateContents_WithAStaleHash_IsAConflict_AndChangesNothing()
    {
        var (_, script) = await SeedAsync("# someone else's newer edit");

        var result = await UpdateAsync(script.Id, "# my edit", ScriptHelper.HashContents("# what I opened"));

        result.ShouldBeOfType<Conflict>();
        (await GetService<ScriptService>().GetAsync(script.Id)).Contents.ShouldBe("# someone else's newer edit");
    }

    [Fact]
    public async Task UpdateContents_UnknownScript_IsNotFound()
    {
        var result = await UpdateAsync(Guid.NewGuid(), "# anything");

        result.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateContents_RequiresAuthentication()
    {
        var (_, script) = await SeedAsync("# before");

        using var anonymous = ApplicationFixture.Instance.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            $"/api/Scripts/{script.Id}/Contents",
            new SDK.Models.UpdateScriptContentsRequest { Contents = "# hijacked" });

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await GetService<ScriptService>().GetAsync(script.Id)).Contents.ShouldBe("# before");
    }
}
