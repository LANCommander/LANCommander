using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Covers the install/modify dialog's add-on filtering (part of the MEDIUM "archive resolve
/// failures abort install/modify/move" finding).
///
/// Install-plan generation skips an add-on the server has no archive for — it can never be
/// downloaded — so offering it in the dialog only lets the user pick something impossible. Tools
/// already applied exactly this <c>Archives?.Any()</c> filter at every call site; add-ons did not.
/// The one carve-out is an add-on that is already installed locally: it must stay listed so that
/// an add-on whose archives were deleted server-side after installation can still be seen and
/// deselected (i.e. uninstalled) in the modify dialog.
/// </summary>
public class InstallOptionsAddonFilterTests
{
    private static SDK.Models.Game MakeAddon(string title, bool hasArchive, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Type = GameType.Expansion,
            Archives = hasArchive
                ? new List<SDK.Models.Archive> { new() { Id = Guid.NewGuid(), Version = "1.0" } }
                : new List<SDK.Models.Archive>(),
        };

    [Fact]
    public void FilterInstallableAddons_DropsAddonsWithNoArchive()
    {
        var withArchive = MakeAddon("Opposing Force", hasArchive: true);
        var withoutArchive = MakeAddon("Blue Shift", hasArchive: false);

        var result = GameActionBarViewModel.FilterInstallableAddons([withArchive, withoutArchive]);

        Assert.Equal([withArchive.Id], result.Select(a => a.Id));
    }

    [Fact]
    public void FilterInstallableAddons_TreatsNullArchiveCollectionAsNoArchive()
    {
        var addon = new SDK.Models.Game { Id = Guid.NewGuid(), Title = "Broken", Archives = null };

        Assert.Empty(GameActionBarViewModel.FilterInstallableAddons([addon]));
    }

    [Fact]
    public void FilterInstallableAddons_KeepsAlreadyInstalledAddonsEvenWithoutAnArchive()
    {
        // An installed add-on whose archives were deleted server-side must remain visible in the
        // modify dialog, otherwise deselecting/uninstalling it would become impossible — and it
        // would silently be dropped from the selection and uninstalled behind the user's back.
        var installedWithoutArchive = MakeAddon("Blue Shift", hasArchive: false);
        var notInstalledWithoutArchive = MakeAddon("Decay", hasArchive: false);

        var result = GameActionBarViewModel.FilterInstallableAddons(
            [installedWithoutArchive, notInstalledWithoutArchive],
            new HashSet<Guid> { installedWithoutArchive.Id });

        Assert.Equal([installedWithoutArchive.Id], result.Select(a => a.Id));
    }

    [Fact]
    public void FilterInstallableAddons_HandlesNullInput()
    {
        Assert.Empty(GameActionBarViewModel.FilterInstallableAddons(null));
    }
}
