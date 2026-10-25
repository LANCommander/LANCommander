using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.Launcher.ViewModels;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

public class InterposerSettingsViewModelTests
{
    private static InheritedOptionValue BuiltIn(string key) => new(InterposerOptions.GetDefault(key), null);

    private static IEnumerable<InterposerOptionRowViewModel> AllRows(InterposerSettingsViewModel vm) =>
        vm.Featured.Concat(vm.Advanced.SelectMany(g => g.Rows));

    [Fact]
    public void NothingIsStoredUntilThePlayerChangesSomething()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(), BuiltIn);

        Assert.Empty(vm.CollectValues());
    }

    [Fact]
    public void WhatThePlayerSetIsShownAndKept()
    {
        var values = new Dictionary<string, string>
        {
            ["RichPresence.Discord.Enabled"] = "True",
            ["OsVersion.Version"] = "WindowsXP",
            ["NetworkRelay.AddressTimeout"] = "2000",
            ["Player.ComputerName"] = "LANPARTY",
            ["FastDL.AllowedExtensions"] = "[]",
        };

        var vm = InterposerSettingsViewModel.Build(values, BuiltIn);

        var presence = Assert.IsType<InterposerChoiceRowViewModel>(vm.Featured.Single(r => r.Key == "RichPresence.Discord.Enabled"));
        Assert.Equal("On", presence.SelectedItem.Label);

        // Stored back in canonical form, and an empty list is a value of its own
        Assert.Equal(new Dictionary<string, string?>
        {
            ["RichPresence.Discord.Enabled"] = "true",
            ["OsVersion.Version"] = "WindowsXP",
            ["NetworkRelay.AddressTimeout"] = "2000",
            ["Player.ComputerName"] = "LANPARTY",
            ["FastDL.AllowedExtensions"] = "[]",
        }, vm.CollectValues());
    }

    [Fact]
    public void DefaultNamesWhatTheOptionInheritsAndFromWhere()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(),
            key => key == "Logging.Files" ? new InheritedOptionValue("true", "the server") : BuiltIn(key));

        var files = Assert.IsType<InterposerChoiceRowViewModel>(AllRows(vm).Single(r => r.Key == "Logging.Files"));

        Assert.Equal("Default (On)", files.SelectedItem.Label);
        Assert.Equal("Default set by the server", files.InheritedSourceText);

        var version = Assert.IsType<InterposerChoiceRowViewModel>(vm.Featured.Single(r => r.Key == "OsVersion.Version"));
        Assert.StartsWith("Default (None", version.SelectedItem.Label);
    }

    [Fact]
    public void ChoosingDefaultAgainStopsStoringTheValue()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string> { ["OsVersion.Version"] = "Windows7" }, BuiltIn);

        var version = Assert.IsType<InterposerChoiceRowViewModel>(vm.Featured.Single(r => r.Key == "OsVersion.Version"));
        version.SelectedItem = version.Items[0];

        Assert.Empty(vm.CollectValues());
    }

    [Fact]
    public void FeaturedOptionsAreNotRepeatedUnderAdvanced()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(), BuiltIn);

        var advanced = vm.Advanced.SelectMany(g => g.Rows).Select(r => r.Key).ToList();

        Assert.All(vm.Featured, featured => Assert.DoesNotContain(featured.Key, advanced));
        Assert.Contains("NetworkRelay.Enabled", vm.Featured.Select(r => r.Key));
    }

    [Fact]
    public void ServerAuthoredAndLauncherSuppliedOptionsAreNotOffered()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(), BuiltIn);
        var keys = AllRows(vm).Select(r => r.Key).ToList();

        Assert.DoesNotContain(keys, k => k.StartsWith("Loader."));
        Assert.DoesNotContain(keys, k => k.StartsWith("Registry."));
        Assert.DoesNotContain("FileRedirects", keys);
        Assert.DoesNotContain("DnsRedirects", keys);
        Assert.DoesNotContain("Player.Username", keys);
        Assert.DoesNotContain("NetworkRelay.Ticket", keys);
        Assert.DoesNotContain("NetworkRelay.Server", keys);
    }

    [Fact]
    public void AnExcludedOptionIsNotOffered()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(), BuiltIn, "NetworkRelay.Enabled");

        Assert.DoesNotContain("NetworkRelay.Enabled", AllRows(vm).Select(r => r.Key));
    }

    [Fact]
    public void NestedGroupsAreNamedInTheirOptionsLabels()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(), BuiltIn);

        var richPresence = vm.Advanced.Single(g => g.DisplayName == "Rich Presence");

        Assert.Contains(richPresence.Rows, r => r.Key == "RichPresence.Discord.ApplicationId" && r.DisplayName == "Discord: Application ID");
    }

    [Fact]
    public void AListIsOnlyStoredOnceThePlayerSetsIt()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>(),
            key => key == "FastDL.AllowedExtensions" ? new InheritedOptionValue("[\".pk3\"]", "the server") : BuiltIn(key));

        var extensions = Assert.IsType<InterposerListRowViewModel>(AllRows(vm).Single(r => r.Key == "FastDL.AllowedExtensions"));

        Assert.Equal("Default: 1 item", extensions.InheritedSummary);
        Assert.Empty(vm.CollectValues());

        // Setting it starts from the list it inherits
        extensions.IsSet = true;

        Assert.Equal("[\".pk3\"]", vm.CollectValues()["FastDL.AllowedExtensions"]);
    }
}
