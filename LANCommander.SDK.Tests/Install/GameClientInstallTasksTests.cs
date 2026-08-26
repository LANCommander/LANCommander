using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using SdkGame = LANCommander.SDK.Models.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Regression coverage for the CRITICAL "in-place version change persists metadata without ever
/// installing anything" bug: <see cref="GameClient.ChangeVersionAsync"/>-equivalent callers (the
/// launcher's <c>InstallService.ChangeVersionAsync(inPlace: true)</c>) build a single manually
/// constructed <see cref="InstallPlanItem"/> rather than a full <see cref="GameClient.GenerateInstallPlanAsync"/>
/// plan, so it is easy to forget to populate <see cref="InstallPlanItem.Tasks"/> at all. Extracting
/// the standard task list into <see cref="GameClient.BuildGameInstallTasks"/> — the exact same
/// pure helper <see cref="GameClient.GenerateInstallPlanAsync"/> itself uses for a fresh install —
/// means an in-place transition can never end up with an empty/insufficient task list, and this
/// task-shape assertion can run without any network dependency.
/// </summary>
public class GameClientInstallTasksTests
{
    private static SdkGame MakeGame(bool withScripts = false, bool withManual = false)
    {
        var game = new SdkGame
        {
            Id = Guid.NewGuid(),
            Title = "Half-Life",
            Scripts = withScripts ? new[] { new Script { Id = Guid.NewGuid(), Type = ScriptType.Install } } : Array.Empty<Script>(),
            Media = withManual ? new[] { new Media { Id = Guid.NewGuid(), Type = MediaType.Manual } } : Array.Empty<Media>(),
        };

        return game;
    }

    [Fact]
    public void BuildGameInstallTasks_AlwaysIncludesTheCriticalDownloadAndManifestTasks()
    {
        var game = MakeGame();
        var archiveId = Guid.NewGuid();

        var tasks = GameClient.BuildGameInstallTasks(game, archiveId, "1.0.0");

        var downloadTask = Assert.Single(tasks, t => t.Type == InstallTaskType.DownloadAndExtract);
        Assert.True(downloadTask.IsCritical);
        Assert.True(downloadTask.ReportsProgress);
        Assert.Equal(archiveId.ToString(), downloadTask.Parameters["ArchiveId"]);
        Assert.Equal("1.0.0", downloadTask.Parameters["ArchiveVersion"]);

        var manifestTask = Assert.Single(tasks, t => t.Type == InstallTaskType.WriteManifest);
        Assert.True(manifestTask.IsCritical);

        Assert.Contains(tasks, t => t.Type == InstallTaskType.VerifyFiles);
        Assert.Contains(tasks, t => t.Type == InstallTaskType.WriteScripts);
        Assert.Contains(tasks, t => t.Type == InstallTaskType.DownloadSaves);
    }

    [Fact]
    public void BuildGameInstallTasks_OmitsScriptTasks_WhenTheGameHasNoScripts()
    {
        var game = MakeGame(withScripts: false);

        var tasks = GameClient.BuildGameInstallTasks(game, Guid.NewGuid(), "1.0.0");

        Assert.DoesNotContain(tasks, t => t.Type == InstallTaskType.RunInstallScript);
        Assert.DoesNotContain(tasks, t => t.Type == InstallTaskType.RunKeyChangeScript);
        Assert.DoesNotContain(tasks, t => t.Type == InstallTaskType.RunNameChangeScript);
    }

    [Fact]
    public void BuildGameInstallTasks_IncludesScriptTasks_WhenTheGameHasScripts()
    {
        var game = MakeGame(withScripts: true);

        var tasks = GameClient.BuildGameInstallTasks(game, Guid.NewGuid(), "1.0.0");

        Assert.Contains(tasks, t => t.Type == InstallTaskType.RunInstallScript);
        Assert.Contains(tasks, t => t.Type == InstallTaskType.RunKeyChangeScript);
        Assert.Contains(tasks, t => t.Type == InstallTaskType.RunNameChangeScript);
    }

    [Fact]
    public void BuildGameInstallTasks_IncludesManualDownload_OnlyWhenTheGameHasManuals()
    {
        var withManual = GameClient.BuildGameInstallTasks(MakeGame(withManual: true), Guid.NewGuid(), "1.0.0");
        var withoutManual = GameClient.BuildGameInstallTasks(MakeGame(withManual: false), Guid.NewGuid(), "1.0.0");

        Assert.Contains(withManual, t => t.Type == InstallTaskType.DownloadManual);
        Assert.DoesNotContain(withoutManual, t => t.Type == InstallTaskType.DownloadManual);
    }

    [Fact]
    public void BuildGameInstallTasks_ToleratesANullArchiveId()
    {
        // GenerateInstallPlanAsync passes resolvedArchive?.Id/Version through as-is, which can be
        // null if no archive could be resolved at all — must not throw, and must still produce a
        // full task list (the download task just carries an empty ArchiveId parameter).
        var game = MakeGame();

        var tasks = GameClient.BuildGameInstallTasks(game, null, null);

        var downloadTask = Assert.Single(tasks, t => t.Type == InstallTaskType.DownloadAndExtract);
        Assert.Equal(string.Empty, downloadTask.Parameters["ArchiveId"]);
        Assert.Equal(string.Empty, downloadTask.Parameters["ArchiveVersion"]);
    }

    [Fact]
    public void BuildGameInstallTasks_OrdersTasksSequentiallyStartingAtZero()
    {
        var game = MakeGame(withScripts: true, withManual: true);

        var tasks = GameClient.BuildGameInstallTasks(game, Guid.NewGuid(), "1.0.0");

        var orders = tasks.OrderBy(t => t.Order).Select(t => t.Order).ToArray();
        Assert.Equal(Enumerable.Range(0, tasks.Count), orders);
    }
}
