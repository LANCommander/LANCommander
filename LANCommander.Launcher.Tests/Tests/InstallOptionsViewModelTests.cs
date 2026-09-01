using System;
using System.Linq;
using ByteSizeLib;
using LANCommander.Launcher.ViewModels;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Covers <see cref="InstallOptionsViewModel"/>'s base-game version selector: default
/// preselection (explicit/effective default, else newest) and size recalculation when the
/// selection changes. Pure view-model logic — no Avalonia rendering or DI container needed,
/// so these stay fast and stable.
/// </summary>
public class InstallOptionsViewModelTests
{
    private static SDK.Models.Archive MakeArchive(
        string version,
        DateTime createdOn,
        long compressedSize,
        long uncompressedSize,
        bool isDefault = false,
        bool isEffectiveDefault = false,
        string? changelog = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Version = version,
            CreatedOn = createdOn,
            CompressedSize = compressedSize,
            UncompressedSize = uncompressedSize,
            IsDefault = isDefault,
            IsEffectiveDefault = isEffectiveDefault,
            Changelog = changelog,
        };

    [Fact]
    public void PopulateArchives_PreselectsExplicitDefault_OverNewerArchive()
    {
        var older = MakeArchive("1.0", DateTime.UtcNow.AddDays(-2), 100, 200, isDefault: true, isEffectiveDefault: true);
        var newer = MakeArchive("2.0", DateTime.UtcNow.AddDays(-1), 300, 400);

        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(new[] { newer, older });

        Assert.NotNull(vm.SelectedArchive);
        Assert.Equal(older.Id, vm.SelectedArchive!.Id);
        Assert.True(vm.ShowVersionSelector);
    }

    [Fact]
    public void PopulateArchives_PreselectsNewest_WhenNoExplicitOrEffectiveDefaultFlagged()
    {
        var older = MakeArchive("1.0", DateTime.UtcNow.AddDays(-2), 100, 200);
        var newer = MakeArchive("2.0", DateTime.UtcNow.AddDays(-1), 300, 400);

        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(new[] { older, newer });

        // No archive is flagged IsEffectiveDefault (e.g. a server that hasn't resolved one) -
        // falls back to the newest by CreatedOn, exactly as the server's own resolver would.
        Assert.NotNull(vm.SelectedArchive);
        Assert.Equal(newer.Id, vm.SelectedArchive!.Id);
    }

    [Fact]
    public void PopulateArchives_PreselectsEffectiveDefault_WhenNotExplicit()
    {
        var older = MakeArchive("1.0", DateTime.UtcNow.AddDays(-2), 100, 200, isEffectiveDefault: true);
        var newer = MakeArchive("2.0", DateTime.UtcNow.AddDays(-1), 300, 400);

        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(new[] { older, newer });

        // "older" is flagged as the effective default (e.g. an admin explicitly pinned it, even
        // though it isn't the newest) - the launcher must trust that flag, not CreatedOn.
        Assert.Equal(older.Id, vm.SelectedArchive!.Id);
    }

    [Fact]
    public void ShowVersionSelector_FalseWithZeroOrOneArchive()
    {
        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(Array.Empty<SDK.Models.Archive>());
        Assert.False(vm.ShowVersionSelector);
        Assert.Null(vm.SelectedArchive);

        var only = MakeArchive("1.0", DateTime.UtcNow, 100, 200, isEffectiveDefault: true);
        vm.PopulateArchives(new[] { only });
        Assert.False(vm.ShowVersionSelector);
        Assert.Equal(only.Id, vm.SelectedArchive!.Id);
    }

    [Fact]
    public void SelectingDifferentArchive_RecalculatesBaseSizes()
    {
        var small = MakeArchive("1.0", DateTime.UtcNow.AddDays(-2), 100, 1_000);
        var large = MakeArchive("2.0", DateTime.UtcNow.AddDays(-1), 900, 9_000, isEffectiveDefault: true);

        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(new[] { small, large });

        // Preselected the effective default ("large") - base sizes must reflect that single
        // archive only, never a sum across every historical archive.
        Assert.Equal(large.Id, vm.SelectedArchive!.Id);
        Assert.Equal(900, vm.BaseDownloadSize);
        Assert.Equal(9_000, vm.BaseSpaceRequired);

        var archiveItem = Assert.Single(vm.Archives, a => a.Id == small.Id);
        vm.SelectedArchive = archiveItem;

        Assert.Equal(100, vm.BaseDownloadSize);
        Assert.Equal(1_000, vm.BaseSpaceRequired);
    }

    [Fact]
    public void SelectingDifferentArchive_ReactivelyUpdatesTotalsIncludingAddonsAndTools()
    {
        var small = MakeArchive("1.0", DateTime.UtcNow.AddDays(-2), 100, 1_000);
        var large = MakeArchive("2.0", DateTime.UtcNow.AddDays(-1), 900, 9_000, isEffectiveDefault: true);

        var vm = new InstallOptionsViewModel();
        vm.PopulateArchives(new[] { small, large });

        var addon = new SDK.Models.Game
        {
            Id = Guid.NewGuid(),
            Title = "Some Addon",
            Archives = new[] { MakeArchive("1.0", DateTime.UtcNow, 50, 500) },
        };
        vm.Addons.Add(new InstallAddonItemViewModel(addon, selectedByDefault: true));
        vm.RefreshSizes();

        // Preselected the effective default ("large", 900) + the selected addon (50) = 950.
        Assert.Equal(ByteSize.FromBytes(950).ToString("0.##"), vm.DownloadSizeText);

        // Switching to the smaller archive (100) must reactively shrink the total to 100 + 50 =
        // 150, proving size recalculation is live rather than computed once at population time.
        vm.SelectedArchive = vm.Archives.Single(a => a.Id == small.Id);

        Assert.Equal(ByteSize.FromBytes(150).ToString("0.##"), vm.DownloadSizeText);
    }

    [Fact]
    public void SelectedArchiveId_ReflectsSelectedArchive_OrNullWhenNone()
    {
        var vm = new InstallOptionsViewModel();
        Assert.Null(vm.SelectedArchiveId);

        var archive = MakeArchive("1.0", DateTime.UtcNow, 100, 200, isEffectiveDefault: true);
        vm.PopulateArchives(new[] { archive });

        Assert.Equal(archive.Id, vm.SelectedArchiveId);
    }
}
