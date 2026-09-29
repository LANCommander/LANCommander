using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Tool install plans never emitted a WriteScripts task, so a tool's scripts were never written to
/// disk. Every tool script runner gates on the file existing, which turned the tool's Install script
/// (and its Before Start / After Stop / Uninstall scripts) into silent no-ops.
/// </summary>
public class ToolClientInstallPlanTests
{
    // GenerateInstallPlanAsync only reads the Tool it is handed, so the dependencies stay null.
    private static ToolClient CreateClient() => new(null!, null!, null!, null!);

    private static Tool ToolWith(params ScriptType[] scriptTypes) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Tool",
        Scripts = scriptTypes
            .Select(t => new Script { Id = Guid.NewGuid(), Type = t, Contents = "# noop" })
            .ToList(),
    };

    private static async Task<List<InstallTaskDefinition>> TasksFor(Tool tool)
    {
        var plan = await CreateClient().GenerateInstallPlanAsync(tool, Path.Combine(Path.GetTempPath(), "lc-tool-plan"));

        return plan.Items.Single().Tasks;
    }

    [Fact]
    public async Task GenerateInstallPlan_EmitsWriteScripts()
    {
        var tasks = await TasksFor(ToolWith(ScriptType.Install));

        Assert.Contains(tasks, t => t.Type == InstallTaskType.WriteScripts);
    }

    /// <summary>Scripts have to be on disk before the install script is invoked.</summary>
    [Fact]
    public async Task GenerateInstallPlan_OrdersWriteScriptsBeforeRunInstallScript()
    {
        var tasks = await TasksFor(ToolWith(ScriptType.Install));

        var write = tasks.Single(t => t.Type == InstallTaskType.WriteScripts);
        var run = tasks.Single(t => t.Type == InstallTaskType.RunInstallScript);

        Assert.True(write.Order < run.Order, "WriteScripts must precede RunInstallScript");
    }

    /// <summary>
    /// DownloadAndExtract deletes the destination when extraction fails, and for a tool the
    /// destination is the game's install directory — scripts written beforehand would be wiped.
    /// </summary>
    [Fact]
    public async Task GenerateInstallPlan_OrdersWriteScriptsAfterDownloadAndManifest()
    {
        var tasks = await TasksFor(ToolWith(ScriptType.Install));

        var write = tasks.Single(t => t.Type == InstallTaskType.WriteScripts);

        Assert.True(tasks.Single(t => t.Type == InstallTaskType.DownloadAndExtract).Order < write.Order);
        Assert.True(tasks.Single(t => t.Type == InstallTaskType.WriteManifest).Order < write.Order);
    }

    /// <summary>
    /// Before Start / After Stop / Uninstall scripts also have to reach disk, so the task is
    /// emitted regardless of whether the tool happens to have an Install script.
    /// </summary>
    [Theory]
    [InlineData(ScriptType.BeforeStart)]
    [InlineData(ScriptType.AfterStop)]
    [InlineData(ScriptType.Uninstall)]
    public async Task GenerateInstallPlan_EmitsWriteScripts_WithoutAnInstallScript(ScriptType scriptType)
    {
        var tasks = await TasksFor(ToolWith(scriptType));

        Assert.Contains(tasks, t => t.Type == InstallTaskType.WriteScripts);
        Assert.DoesNotContain(tasks, t => t.Type == InstallTaskType.RunInstallScript);
    }

    [Fact]
    public async Task GenerateInstallPlan_EmitsWriteScripts_WhenToolHasNoScripts()
    {
        var tool = new Tool { Id = Guid.NewGuid(), Name = "Bare", Scripts = new List<Script>() };

        var tasks = await TasksFor(tool);

        Assert.Contains(tasks, t => t.Type == InstallTaskType.WriteScripts);
        Assert.DoesNotContain(tasks, t => t.Type == InstallTaskType.RunInstallScript);
    }

    [Fact]
    public async Task GenerateInstallPlan_TargetsTheToolForEveryTask()
    {
        var tool = ToolWith(ScriptType.Install);

        var tasks = await TasksFor(tool);

        Assert.All(tasks, t => Assert.Equal(tool.Id, t.TargetId));
    }

    /// <summary>Guards the hand-maintained taskOrder++ sequence against a duplicated order.</summary>
    [Fact]
    public async Task GenerateInstallPlan_AssignsDistinctTaskOrders()
    {
        var tasks = await TasksFor(ToolWith(ScriptType.Install));

        Assert.Equal(tasks.Count, tasks.Select(t => t.Order).Distinct().Count());
    }
}
