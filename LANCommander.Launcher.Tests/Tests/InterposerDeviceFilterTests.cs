using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.Launcher.Services.Platform;
using LANCommander.Launcher.ViewModels;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

public class InterposerDeviceFilterTests
{
    private static InheritedOptionValue BuiltIn(string key) => new(InterposerOptions.GetDefault(key), null);

    private static readonly NetworkAdapterDevice Lan = new("Ethernet", "Intel(R) Ethernet Controller I225-V", "58:11:22:4E:39:FA", "192.168.1.20", 24, true);
    private static readonly NetworkAdapterDevice ZeroTier = new("ZeroTier One", "ZeroTier Virtual Port", "22:CD:1A:EE:A7:2E", "10.147.17.5", 24, true);

    private static readonly AudioDevice Speakers = new("{0.0.0.00000000}.{speakers}", "Speakers (Realtek(R) Audio)", AudioDirection.Output, true);
    private static readonly AudioDevice Monitor = new("{0.0.0.00000000}.{monitor}", "VG240Y (NVIDIA High Definition Audio)", AudioDirection.Output, false);
    private static readonly AudioDevice Headset = new("{0.0.1.00000000}.{headset}", "Microphone (Arctis 7)", AudioDirection.Input, true);

    private static readonly ControllerDevice XboxPad = new("Controller (Xbox One For Windows)", "Controller (Xbox One For Windows)", "Gamepad");

    private sealed class FakeCatalog : IDeviceCatalog
    {
        public IReadOnlyList<NetworkAdapterDevice> GetNetworkAdapters() => [Lan, ZeroTier];
        public IReadOnlyList<AudioDevice> GetAudioDevices() => [Speakers, Monitor, Headset];
        public IReadOnlyList<ControllerDevice> GetControllers() => [XboxPad];
    }

    private static async Task<InterposerSettingsViewModel> Editor(Dictionary<string, string>? values = null, System.Func<string, InheritedOptionValue>? inherit = null)
    {
        var vm = InterposerSettingsViewModel.Build(values ?? new(), inherit ?? BuiltIn);
        await vm.LoadDevicesAsync(new FakeCatalog());
        return vm;
    }

    private static T Filter<T>(InterposerSettingsViewModel vm) => vm.Devices.OfType<T>().Single();

    private static void Select<T>(T filter) where T : InterposerDeviceFilterViewModel =>
        filter.SelectedMode = filter.Modes.Single(m => m.Mode == DeviceFilterMode.Selected);

    private static List<string> List(Dictionary<string, string?> values, string key) =>
        JsonSerializer.Deserialize<List<string>>(values[key]!)!;

    [Fact]
    public async Task EveryFilterStartsAtDefaultAndStoresNothing()
    {
        var vm = await Editor();

        Assert.Equal(3, vm.Devices.Count);
        Assert.All(vm.Devices, f => Assert.Equal(DeviceFilterMode.Default, f.SelectedMode.Mode));
        Assert.Empty(vm.CollectValues());
    }

    [Fact]
    public async Task ShowAllTurnsTheFilterOff()
    {
        var vm = await Editor();
        var adapters = Filter<InterposerNetworkAdapterFilterViewModel>(vm);

        adapters.SelectedMode = adapters.Modes.Single(m => m.Mode == DeviceFilterMode.All);

        Assert.Equal(new Dictionary<string, string?> { ["NetworkAdapters.Enabled"] = "false" }, vm.CollectValues());
    }

    [Fact]
    public async Task APickedAdapterIsStoredAsItsSubnetAndAddress()
    {
        var vm = await Editor();
        var filter = Filter<InterposerNetworkAdapterFilterViewModel>(vm);

        Select(filter);
        filter.Adapters.Single(a => a.Label == "Ethernet").IsSelected = true;

        var values = vm.CollectValues();

        Assert.Equal("true", values["NetworkAdapters.Enabled"]);
        Assert.Equal(["192.168.1.0/24"], List(values, "NetworkAdapters.Subnets"));
        Assert.Equal(["58:11:22:4E:39:FA"], List(values, "NetworkAdapters.MACs"));
        Assert.Empty(List(values, "NetworkAdapters.Names"));
    }

    [Fact]
    public async Task StoredAdaptersArePickedAgainAndRulesForOthersAreKept()
    {
        var vm = await Editor(new()
        {
            ["NetworkAdapters.Enabled"] = "true",
            ["NetworkAdapters.Subnets"] = "[\"10.147.17.0/24\",\"172.16.0.0/12\"]",
            ["NetworkAdapters.MACs"] = "[]",
            ["NetworkAdapters.Names"] = "[\"^Realtek\"]",
        });

        var filter = Filter<InterposerNetworkAdapterFilterViewModel>(vm);

        Assert.True(filter.IsSelecting);
        Assert.True(filter.Adapters.Single(a => a.Label == "ZeroTier One").IsSelected);
        Assert.False(filter.Adapters.Single(a => a.Label == "Ethernet").IsSelected);
        Assert.Equal(["Subnet 172.16.0.0/12", "Name matching ^Realtek"], filter.OtherRules);

        var values = vm.CollectValues();

        Assert.Equal(["10.147.17.0/24", "172.16.0.0/12"], List(values, "NetworkAdapters.Subnets"));
        Assert.Equal(["22:CD:1A:EE:A7:2E"], List(values, "NetworkAdapters.MACs"));
        Assert.Equal(["^Realtek"], List(values, "NetworkAdapters.Names"));
    }

    [Fact]
    public async Task PickingControllersStartsFromMiceAndKeyboards()
    {
        var vm = await Editor();
        var filter = Filter<InterposerControllerFilterViewModel>(vm);

        Select(filter);

        var values = vm.CollectValues();

        Assert.Equal(["Mouse", "Keyboard"], List(values, "DirectInput.DeviceFilter.Classes"));
        Assert.Empty(List(values, "DirectInput.DeviceFilter.Names"));
    }

    [Fact]
    public async Task APickedControllerIsStoredAsAnExactNamePattern()
    {
        var vm = await Editor();
        var filter = Filter<InterposerControllerFilterViewModel>(vm);

        Select(filter);
        filter.Controllers.Single().IsSelected = true;

        Assert.Equal([@"^Controller \(Xbox One For Windows\)$"], List(vm.CollectValues(), "DirectInput.DeviceFilter.Names"));
    }

    [Fact]
    public async Task AControllerThatIsNotConnectedKeepsItsRule()
    {
        var vm = await Editor(new()
        {
            ["DirectInput.DeviceFilter.Enabled"] = "true",
            ["DirectInput.DeviceFilter.Classes"] = "[\"Mouse\"]",
            ["DirectInput.DeviceFilter.Names"] = @"[""^Logitech G29 Driving Force Racing Wheel$"",""^Controller \\(Xbox One For Windows\\)$""]",
        });

        var filter = Filter<InterposerControllerFilterViewModel>(vm);

        Assert.True(filter.Controllers.Single(c => c.Label == "Controller (Xbox One For Windows)").IsSelected);

        var wheel = filter.Controllers.Single(c => c.Label == "Logitech G29 Driving Force Racing Wheel");
        Assert.True(wheel.IsSelected);
        Assert.Equal("Not connected", wheel.Detail);

        Assert.Equal(2, List(vm.CollectValues(), "DirectInput.DeviceFilter.Names").Count);
    }

    [Theory]
    [InlineData("Controller (Xbox One For Windows)")]
    [InlineData(@"Weird\Name [v1.2] {x}+?*^$|/")]
    [InlineData("Plain Name")]
    public void ExactPatternsReadBack(string name)
    {
        var pattern = InterposerControllerFilterViewModel.ExactPattern(name);

        Assert.Equal(name, InterposerControllerFilterViewModel.FromExactPattern(pattern));
        Assert.Matches(pattern, name);
        Assert.DoesNotContain(@"\ ", pattern);
    }

    [Fact]
    public void AHandWrittenPatternIsNotReadAsAName()
    {
        Assert.Null(InterposerControllerFilterViewModel.FromExactPattern("^Xbox.*"));
        Assert.Null(InterposerControllerFilterViewModel.FromExactPattern("Wireless"));
    }

    [Fact]
    public async Task PickedAudioDevicesAreStoredByEndpointIdPerDirection()
    {
        var vm = await Editor();
        var filter = Filter<InterposerAudioFilterViewModel>(vm);

        Select(filter);

        filter.Output.SelectedMode = filter.Output.Modes.Single(m => m.Mode == AudioDirectionMode.Selected);
        filter.Output.Devices.Single(d => d.Label.StartsWith("Speakers")).IsSelected = true;

        filter.Input.SelectedMode = filter.Input.Modes.Single(m => m.Mode == AudioDirectionMode.None);

        var values = vm.CollectValues();

        Assert.Equal("true", values["AudioDevices.Enabled"]);
        Assert.Equal([Speakers.Id], List(values, "AudioDevices.Output"));
        Assert.Equal("[]", values["AudioDevices.Input"]);
    }

    [Fact]
    public async Task ADirectionLeftUnfilteredTakesBackTheListItInherits()
    {
        var vm = await Editor(inherit: key => key switch
        {
            "AudioDevices.Enabled" => new("true", "the server"),
            "AudioDevices.Input" => new("[]", "the server"),
            _ => BuiltIn(key),
        });

        var filter = Filter<InterposerAudioFilterViewModel>(vm);

        Assert.Equal("Default (only some shown)", filter.SelectedMode.Label);
        Assert.Equal("Default set by the server", filter.InheritedSourceText);

        // Picking starts from what applies: the server hides every microphone
        Select(filter);
        Assert.Equal(AudioDirectionMode.None, filter.Input.SelectedMode.Mode);

        filter.Input.SelectedMode = filter.Input.Modes.Single(m => m.Mode == AudioDirectionMode.All);

        var values = vm.CollectValues();

        // Stored as a removal, which the DLL reads as "do not filter microphones"
        Assert.True(values.ContainsKey("AudioDevices.Input"));
        Assert.Null(values["AudioDevices.Input"]);
        Assert.Null(values["AudioDevices.Output"]);
    }

    [Fact]
    public async Task AnAudioDeviceThatIsNotConnectedKeepsItsEntry()
    {
        var vm = await Editor(new()
        {
            ["AudioDevices.Enabled"] = "true",
            ["AudioDevices.Output"] = "[\"Speakers (Realtek(R) Audio)\",\"USB\\\\VID_0965&PID_9001\"]",
        });

        var output = Filter<InterposerAudioFilterViewModel>(vm).Output;

        Assert.True(output.Devices.Single(d => d.Label.StartsWith("Speakers")).IsSelected);
        Assert.True(output.Devices.Single(d => d.Label == @"USB\VID_0965&PID_9001").IsSelected);

        // The connected device is now stored by its id; the other stays as it was written
        Assert.Equal([Speakers.Id, @"USB\VID_0965&PID_9001"], List(vm.CollectValues(), "AudioDevices.Output"));
    }

    [Fact]
    public void BeforeTheDevicesAreListedTheStoredRulesAreKeptAsTheyAre()
    {
        var vm = InterposerSettingsViewModel.Build(new Dictionary<string, string>
        {
            ["AudioDevices.Enabled"] = "true",
            ["AudioDevices.Output"] = "[\"{0.0.0.00000000}.{speakers}\"]",
            ["NetworkAdapters.Enabled"] = "true",
            ["NetworkAdapters.Subnets"] = "[\"10.0.0.0/8\"]",
        }, BuiltIn);

        var values = vm.CollectValues();

        Assert.Equal(["{0.0.0.00000000}.{speakers}"], List(values, "AudioDevices.Output"));
        Assert.Equal(["10.0.0.0/8"], List(values, "NetworkAdapters.Subnets"));
    }

    [Fact]
    public async Task TheRawFilterListsAreNotAlsoOfferedUnderAdvanced()
    {
        var vm = await Editor();
        var keys = vm.Advanced.SelectMany(g => g.Rows).Select(r => r.Key).ToList();

        Assert.DoesNotContain(keys, k => k.StartsWith("NetworkAdapters.") || k.StartsWith("AudioDevices.") || k.StartsWith("DirectInput.DeviceFilter."));
        Assert.Contains("DirectInput.FixLegacyDeviceEnumeration", vm.Featured.Select(r => r.Key));
    }
}
