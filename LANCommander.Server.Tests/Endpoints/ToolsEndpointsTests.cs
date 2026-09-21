using System.Net;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// The tool scripts endpoint was defined but never mapped, and its query filtered on
/// RedistributableId instead of ToolId, so a launcher could never retrieve a tool's scripts.
/// </summary>
/// <remarks>
/// The handlers are invoked directly rather than over HTTP. They are the code under test, and this
/// keeps these cases off the fixture's shared authentication token, which other test classes in the
/// collection mutate.
/// </remarks>
[Collection("Application")]
public class ToolsEndpointsTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    /// <summary>
    /// Seeds a tool owning the given scripts. Each test seeds its own tool so the per-tool response
    /// cache cannot leak between them.
    /// </summary>
    private async Task<Tool> SeedToolAsync(params (ScriptType Type, string Contents)[] scripts)
    {
        var toolService = GetService<ToolService>();
        var scriptService = GetService<ScriptService>();

        var tool = await toolService.AddAsync(new Tool { Name = $"Test Tool {Guid.NewGuid()}" });

        // Attach scripts by ToolId rather than through Tool.Scripts: AddAsync reconciles that
        // navigation against existing rows, so brand new scripts would not be persisted.
        foreach (var script in scripts)
        {
            await scriptService.AddAsync(new Script
            {
                Name = script.Type.ToString(),
                Type = script.Type,
                Contents = script.Contents,
                ToolId = tool.Id,
            });
        }

        return tool;
    }

    private async Task<List<SDK.Models.Script>> GetScriptsAsync(Guid toolId)
    {
        var result = await ToolsEndpoints.GetScriptsByIdAsync(
            GetService<ScriptService>(),
            GetService<IFusionCache>(),
            GetService<SdkMapper>(),
            toolId);

        return result
            .ShouldBeOfType<Ok<List<SDK.Models.Script>>>()
            .Value!
            .ToList();
    }

    /// <summary>
    /// The handler existed but was never added to the route group, so every request 404'd before
    /// reaching it.
    /// </summary>
    [Fact]
    public async Task GetScripts_RouteIsMapped()
    {
        var tool = await SeedToolAsync((ScriptType.Install, "# install"));

        var response = await ApplicationFixture.Instance.HttpClient.GetAsync($"/api/Tools/{tool.Id}/Scripts");

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetScripts_ReturnsToolOwnedScriptsWithContents()
    {
        var tool = await SeedToolAsync(
            (ScriptType.Install, "# install"),
            (ScriptType.BeforeStart, "# before start"));

        var scripts = await GetScriptsAsync(tool.Id);

        scripts.Count.ShouldBe(2);
        scripts.ShouldContain(s => s.Type == ScriptType.Install && s.Contents == "# install");
        scripts.ShouldContain(s => s.Type == ScriptType.BeforeStart && s.Contents == "# before start");
    }

    /// <summary>Package scripts are a server-side packaging concern and must not reach a launcher.</summary>
    [Fact]
    public async Task GetScripts_ExcludesPackageScripts()
    {
        var tool = await SeedToolAsync(
            (ScriptType.Install, "# install"),
            (ScriptType.Package, "# package"));

        var scripts = await GetScriptsAsync(tool.Id);

        scripts.ShouldAllBe(s => s.Type != ScriptType.Package);
        scripts.ShouldContain(s => s.Type == ScriptType.Install);
    }

    /// <summary>
    /// The handler filtered on RedistributableId, so it never matched the requested tool and
    /// returned nothing regardless of what the tool owned.
    /// </summary>
    [Fact]
    public async Task GetScripts_DoesNotReturnAnotherToolsScripts()
    {
        var tool = await SeedToolAsync((ScriptType.Install, "# mine"));
        await SeedToolAsync((ScriptType.Install, "# theirs"));

        var scripts = await GetScriptsAsync(tool.Id);

        scripts.ShouldNotBeEmpty();
        scripts.ShouldAllBe(s => s.Contents == "# mine");
    }

    /// <summary>
    /// A redistributable's scripts must not surface as a tool's, which is what the original
    /// predicate would have done for a matching id.
    /// </summary>
    [Fact]
    public async Task GetScripts_DoesNotReturnRedistributableScripts()
    {
        var redistributableService = GetService<RedistributableService>();
        var scriptService = GetService<ScriptService>();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Test Redistributable {Guid.NewGuid()}",
        });

        await scriptService.AddAsync(new Script
        {
            Name = "Install",
            Type = ScriptType.Install,
            Contents = "# redistributable",
            RedistributableId = redistributable.Id,
        });

        // Ask for the redistributable's id as though it were a tool's; nothing should match.
        var scripts = await GetScriptsAsync(redistributable.Id);

        scripts.ShouldBeEmpty();
    }

    /// <summary>
    /// The tool install plan sources script contents from the detail response, so it has to carry
    /// them for tools installed against a server that predates the /Scripts route.
    /// </summary>
    [Fact]
    public async Task GetById_IncludesScriptsWithContents()
    {
        var tool = await SeedToolAsync((ScriptType.Install, "# install"));

        var result = await ToolsEndpoints.GetByIdAsync(
            tool.Id,
            GetService<SdkMapper>(),
            GetService<ToolService>());

        var fetched = result.ShouldBeOfType<Ok<SDK.Models.Tool>>().Value;

        fetched.ShouldNotBeNull();
        fetched.Scripts.ShouldContain(s => s.Type == ScriptType.Install && s.Contents == "# install");
    }
}
