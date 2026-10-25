using LANCommander.Launcher.Services.Interposer;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class InterposerConfigOverlayTests
{
    private static YamlMappingNode Render(params (string Key, string? Value)[] values)
    {
        var yaml = InterposerConfigOverlay.Build(values.ToDictionary(v => v.Key, v => v.Value));

        yaml.ShouldNotBeNull();

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static YamlNode At(YamlMappingNode root, string path)
    {
        YamlNode node = root;

        foreach (var segment in path.Split('.'))
            node = ((YamlMappingNode)node).Children[new YamlScalarNode(segment)];

        return node;
    }

    private static YamlScalarNode Scalar(YamlMappingNode root, string path) => (YamlScalarNode)At(root, path);

    [Fact]
    public void NothingToLayerIsNoOverlay()
    {
        InterposerConfigOverlay.Build(new Dictionary<string, string?>()).ShouldBeNull();
    }

    [Fact]
    public void DottedKeysNestAndShareTheirParents()
    {
        var root = Render(("RichPresence.Discord.Enabled", "true"), ("RichPresence.Discord.ApplicationId", "1234"), ("RichPresence.Details", "Playing"));

        Scalar(root, "RichPresence.Discord.Enabled").Value.ShouldBe("true");
        Scalar(root, "RichPresence.Discord.ApplicationId").Value.ShouldBe("1234");
        Scalar(root, "RichPresence.Details").Value.ShouldBe("Playing");
    }

    [Fact]
    public void BooleansAndNumbersAreWrittenAsSuchAndTextStaysText()
    {
        var root = Render(("NetworkRelay.UDP", "False"), ("NetworkRelay.Port", "41300"), ("Player.Username", "true"));

        var udp = Scalar(root, "NetworkRelay.UDP");
        udp.Value.ShouldBe("false");
        udp.Style.ShouldBe(YamlDotNet.Core.ScalarStyle.Plain);

        Scalar(root, "NetworkRelay.Port").Value.ShouldBe("41300");

        // A username that happens to read as a boolean is still a string
        Scalar(root, "Player.Username").Style.ShouldNotBe(YamlDotNet.Core.ScalarStyle.Plain);
    }

    [Fact]
    public void AnEmptyStringStaysAStringAndANullRemovesTheKey()
    {
        var root = Render(("Player.ComputerName", ""), ("AudioDevices.Input", null));

        var computerName = Scalar(root, "Player.ComputerName");
        computerName.Value.ShouldBe("");
        computerName.Style.ShouldNotBe(YamlDotNet.Core.ScalarStyle.Plain);

        // Read by the DLL as "remove this key"
        var input = Scalar(root, "AudioDevices.Input");
        (input.Value is null or "" or "~" or "null").ShouldBeTrue();
        input.Style.ShouldBe(YamlDotNet.Core.ScalarStyle.Plain);
    }

    [Fact]
    public void ScalarListsAreSequences()
    {
        var root = Render(("DirectInput.DeviceFilter.Classes", "[\"Mouse\",\"Keyboard\"]"), ("AudioDevices.Output", "[]"));

        var classes = (YamlSequenceNode)At(root, "DirectInput.DeviceFilter.Classes");
        classes.Children.Select(c => ((YamlScalarNode)c).Value).ShouldBe(["Mouse", "Keyboard"]);

        // An empty list is a filter that hides everything, not an absent one
        ((YamlSequenceNode)At(root, "AudioDevices.Output")).Children.ShouldBeEmpty();
    }

    [Fact]
    public void CompositeListsAreSequencesOfMapsWithTypedFields()
    {
        var root = Render(
            ("FileRedirects", "[{\"Pattern\":\".*\\\\\\\\saves\\\\\\\\(.+)\",\"Replacement\":\"%SAVEDGAMES%\\\\Game\\\\$1\"}]"),
            ("FastDL.FilteredPorts", "[{\"Min\":\"27000\",\"Max\":27015}]"));

        var redirect = (YamlMappingNode)((YamlSequenceNode)At(root, "FileRedirects")).Children.ShouldHaveSingleItem();
        ((YamlScalarNode)redirect.Children[new YamlScalarNode("Pattern")]).Value.ShouldBe(@".*\\saves\\(.+)");
        ((YamlScalarNode)redirect.Children[new YamlScalarNode("Replacement")]).Value.ShouldBe(@"%SAVEDGAMES%\Game\$1");

        var ports = (YamlMappingNode)((YamlSequenceNode)At(root, "FastDL.FilteredPorts")).Children.ShouldHaveSingleItem();
        var min = (YamlScalarNode)ports.Children[new YamlScalarNode("Min")];
        min.Value.ShouldBe("27000");
        min.Style.ShouldBe(YamlDotNet.Core.ScalarStyle.Plain);
    }
}
