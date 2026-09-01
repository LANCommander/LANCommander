using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers the queue-item identity split introduced for side-by-side installs: IInstallQueueItem.Id
/// is a distinct per-queue-item identity (used for queue lookups/removal/cancellation/progress
/// matching), while EntityId carries the underlying game/tool/redistributable id (used for server
/// lookups and library navigation). Two queue items installing different archives of the same game
/// must share EntityId but never collide on Id, so the queue/UI never dedupes, cancels, or
/// overwrites one version's queue entry because of the other's.
/// </summary>
public class InstallQueueItemIdentityTests
{
    [Fact]
    public void InstallQueueGame_two_versions_of_same_game_have_distinct_queue_ids_but_shared_entity_id()
    {
        var gameId = Guid.NewGuid();
        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();
        var game = new SDK.Models.Game { Id = gameId, Title = "Half-Life" };

        var planItemV1 = new InstallPlanItem
        {
            EntityId = gameId,
            Title = game.Title,
            Type = InstallPlanItemType.Game,
            InstallDirectory = @"C:\Games\HalfLife",
            ArchiveId = archiveA,
            ArchiveVersion = "1.0.0",
        };

        var planItemV2 = new InstallPlanItem
        {
            EntityId = gameId,
            Title = game.Title,
            Type = InstallPlanItemType.Game,
            InstallDirectory = @"C:\Games\HalfLife (1.1.0)",
            ArchiveId = archiveB,
            ArchiveVersion = "1.1.0",
        };

        var queueItemV1 = new InstallQueueGame(planItemV1, game);
        var queueItemV2 = new InstallQueueGame(planItemV2, game);

        // Distinct queue identity — this is what queue lookups/removal/cancellation/progress key on.
        queueItemV1.Id.ShouldNotBe(queueItemV2.Id);
        queueItemV1.Id.ShouldBe(planItemV1.PlanItemId);
        queueItemV2.Id.ShouldBe(planItemV2.PlanItemId);

        // Shared underlying entity — both are still "Half-Life".
        queueItemV1.EntityId.ShouldBe(gameId);
        queueItemV2.EntityId.ShouldBe(gameId);

        // Distinct pinned archive/version and distinct install paths.
        queueItemV1.ArchiveId.ShouldBe(archiveA);
        queueItemV2.ArchiveId.ShouldBe(archiveB);
        queueItemV1.InstallDirectory.ShouldNotBe(queueItemV2.InstallDirectory);
    }

    [Fact]
    public void InstallQueueGame_id_defaults_to_a_fresh_guid_when_not_built_from_a_plan_item()
    {
        var game = new SDK.Models.Game { Id = Guid.NewGuid(), Title = "Quake" };

        var first = new InstallQueueGame(game);
        var second = new InstallQueueGame(game);

        first.Id.ShouldNotBe(Guid.Empty);
        first.Id.ShouldNotBe(second.Id);
        first.EntityId.ShouldBe(game.Id);
        second.EntityId.ShouldBe(game.Id);
    }

    [Fact]
    public void InstallQueueTool_carries_parent_game_id_separately_from_its_own_entity_id()
    {
        var gameId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var tool = new SDK.Models.Tool { Id = toolId, Name = "7-Zip" };

        var planItem = new InstallPlanItem
        {
            EntityId = toolId,
            Title = tool.Name,
            Type = InstallPlanItemType.Tool,
            InstallDirectory = @"C:\Games\HalfLife",
        };

        var queueItem = new InstallQueueTool(planItem, tool) { ParentGameId = gameId };

        queueItem.Id.ShouldBe(planItem.PlanItemId);
        queueItem.EntityId.ShouldBe(toolId);
        queueItem.ParentGameId.ShouldBe(gameId);
        queueItem.Id.ShouldNotBe(queueItem.EntityId);
    }

    [Fact]
    public void DownloadQueueRedistributable_carries_parent_game_id_separately_from_its_own_entity_id()
    {
        var gameId = Guid.NewGuid();
        var redistId = Guid.NewGuid();
        var redist = new SDK.Models.Redistributable { Id = redistId, Name = "DirectX" };

        var planItem = new InstallPlanItem
        {
            EntityId = redistId,
            Title = redist.Name,
            Type = InstallPlanItemType.Redistributable,
            InstallDirectory = @"C:\Games\HalfLife",
        };

        var queueItem = new DownloadQueueRedistributable(planItem, redist) { ParentGameId = gameId };

        queueItem.Id.ShouldBe(planItem.PlanItemId);
        queueItem.EntityId.ShouldBe(redistId);
        queueItem.ParentGameId.ShouldBe(gameId);
    }

    [Fact]
    public void Addon_and_tool_items_depend_on_the_base_game_items_queue_identity_not_its_entity_id()
    {
        // Mirrors GameClient.GenerateInstallPlanAsync's wiring: addon/tool plan items reference
        // the base game plan item's PlanItemId, which becomes the base game queue item's Id.
        var gameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var game = new SDK.Models.Game { Id = gameId, Title = "Half-Life" };
        var addon = new SDK.Models.Game { Id = addonId, Title = "Opposing Force" };

        var gamePlanItem = new InstallPlanItem { EntityId = gameId, Type = InstallPlanItemType.Game };
        var addonPlanItem = new InstallPlanItem
        {
            EntityId = addonId,
            Type = InstallPlanItemType.Addon,
            DependsOnId = gamePlanItem.PlanItemId,
        };

        var gameQueueItem = new InstallQueueGame(gamePlanItem, game);
        var addonQueueItem = new InstallQueueGame(addonPlanItem, addon);

        addonQueueItem.DependsOnId.ShouldBe(gameQueueItem.Id);
        addonQueueItem.DependsOnId.ShouldNotBe(gameQueueItem.EntityId);
    }
}
