using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.ViewModels.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Covers action routing in the consolidated game menu (<see cref="GameContextMenu"/>): the new
/// "Install Another Version" and "Change Version" items are bound to the right commands and are
/// visible/hidden with the same install state every other installed-only action already uses, and
/// the version/path-scoped wording for "Uninstall"/"Change Version" reflects the selected
/// installation once a game has more than one side-by-side installation.
/// </summary>
public class GameContextMenuTests
{
    private static readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
        .BuildServiceProvider();

    private static GameInstallationItemViewModel MakeInstallationItem(string version, string installDirectory, bool isSelected = false) =>
        new(new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            Version = version,
            InstallDirectory = installDirectory,
            IsSelected = isSelected,
        });

    [AvaloniaFact]
    public void InstallAnotherVersionAndChangeVersion_AreVisible_AndBoundToTheirCommands_WhenInstalled()
    {
        var vm = new GameActionBarViewModel(_services) { IsInstalled = true };

        var flyout = GameContextMenu.CreateFlyout(vm);
        var items = flyout.Items.OfType<MenuItem>().ToList();

        var installAnother = items.Single(i => ReferenceEquals(i.Command, vm.InstallAnotherVersionCommand));
        Assert.True(installAnother.IsVisible);

        var changeVersion = items.Single(i => ReferenceEquals(i.Command, vm.ChangeVersionCommand));
        Assert.True(changeVersion.IsVisible);
    }

    [AvaloniaFact]
    public void InstallAnotherVersionAndChangeVersion_AreHidden_WhenNotInstalled()
    {
        var vm = new GameActionBarViewModel(_services) { IsInstalled = false };

        var flyout = GameContextMenu.CreateFlyout(vm);
        var items = flyout.Items.OfType<MenuItem>().ToList();

        var installAnother = items.Single(i => ReferenceEquals(i.Command, vm.InstallAnotherVersionCommand));
        Assert.False(installAnother.IsVisible);

        var changeVersion = items.Single(i => ReferenceEquals(i.Command, vm.ChangeVersionCommand));
        Assert.False(changeVersion.IsVisible);
    }

    [AvaloniaFact]
    public void Uninstall_UsesPlainLabel_WithOnlyOneInstallation()
    {
        var vm = new GameActionBarViewModel(_services) { IsInstalled = true };
        vm.Installations.Add(MakeInstallationItem("1.0.0", @"C:\Games\Foo", isSelected: true));
        vm.SelectedInstallationItem = vm.Installations[0];

        var flyout = GameContextMenu.CreateFlyout(vm);
        var uninstall = flyout.Items.OfType<MenuItem>().Single(i => ReferenceEquals(i.Command, vm.UninstallCommand));

        Assert.Equal("Uninstall", uninstall.Header);
    }

    [AvaloniaFact]
    public void Uninstall_NamesTheSelectedVersionAndPath_WithMultipleInstallations()
    {
        var vm = new GameActionBarViewModel(_services) { IsInstalled = true };
        vm.Installations.Add(MakeInstallationItem("1.0.0", @"C:\Games\Foo", isSelected: true));
        vm.Installations.Add(MakeInstallationItem("2.0.0", @"C:\Games\Foo (2.0.0)"));
        vm.SelectedInstallationItem = vm.Installations[0];

        var flyout = GameContextMenu.CreateFlyout(vm);
        var uninstall = flyout.Items.OfType<MenuItem>().Single(i => ReferenceEquals(i.Command, vm.UninstallCommand));
        var changeVersion = flyout.Items.OfType<MenuItem>().Single(i => ReferenceEquals(i.Command, vm.ChangeVersionCommand));

        Assert.Contains("This Version", uninstall.Header as string);
        Assert.Contains(@"C:\Games\Foo", uninstall.Header as string);
        Assert.Contains(@"C:\Games\Foo", changeVersion.Header as string);
    }
}
