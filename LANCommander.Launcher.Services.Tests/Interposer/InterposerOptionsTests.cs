using LANCommander.Launcher.Services.Interposer;
using LANCommander.SDK.Models;
using Shouldly;
using Xunit;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestRedistributable = LANCommander.SDK.Models.Manifest.Redistributable;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class InterposerOptionsTests
{
    private static Dictionary<string, string> Values(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    [Fact]
    public void TheBundledSchemaIsTheRedistributables()
    {
        InterposerOptions.RedistributableId.ShouldBe(Guid.Parse("2180362b-b469-4cbc-be5d-9d0baae164a4"));

        InterposerOptions.Definitions.ShouldContainKey("NetworkRelay.Enabled");
        InterposerOptions.Definitions.ShouldContainKey("RichPresence.Discord.ApplicationId");
        InterposerOptions.GetDefault("OsVersion.Version").ShouldBe("None");
    }

    [Fact]
    public void ThePlayersValueWinsThenTheLaunchersThenTheServersThenTheDefault()
    {
        var options = new InterposerOptions(
            Values(("OsVersion.Version", "Windows2000"), ("Logging.Files", "true"), ("Player.ComputerName", "SERVER")),
            Values(("OsVersion.Version", "WindowsXP"), ("Logging.Files", "false")),
            Values(("OsVersion.Version", "Windows7")),
            true);

        options.Get("OsVersion.Version").ShouldBe("Windows7");
        options.Get("Logging.Files").ShouldBe("false");
        options.Get("Player.ComputerName").ShouldBe("SERVER");
        options.Get("NetworkRelay.Port").ShouldBe("41300");
        options.IsEnabled("Logging.Files").ShouldBeFalse();
    }

    [Fact]
    public void TheLaunchersCopyIsGivenTheServersValuesAndTheRedistributablesIsNot()
    {
        var options = new InterposerOptions(
            Values(("Player.ComputerName", "SERVER"), ("Loader.Method", "Proxy")),
            Values(("Logging.Files", "true")),
            Values(("OsVersion.Version", "WindowsXP")),
            true);

        var bundled = options.GetOverlayValues(bundled: true);
        bundled.Keys.ShouldBe(["Player.ComputerName", "Logging.Files", "OsVersion.Version"], ignoreOrder: true);

        // Its Config.yml already has them
        var installed = options.GetOverlayValues(bundled: false);
        installed.Keys.ShouldBe(["Logging.Files", "OsVersion.Version"], ignoreOrder: true);
    }

    [Fact]
    public void LoaderOptionsAreNeverPartOfTheConfig()
    {
        var options = new InterposerOptions(null, null, Values(("Loader.Method", "Injector")), false);

        options.GetOverlayValues(bundled: true).ShouldBeEmpty();
        options.ChangesConfig(bundled: true).ShouldBeFalse();
    }

    [Fact]
    public void ValuesThatMatchWhatTheDllWouldDoAnywayChangeNothing()
    {
        // Defaults spelled out, and a blank where the default is no value at all
        var options = new InterposerOptions(null, null, Values(("NetworkRelay.UDP", "True"), ("OsVersion.Version", "None"), ("Player.Username", "")), false);

        options.ChangesConfig(bundled: true).ShouldBeFalse();
    }

    [Fact]
    public void ForTheRedistributablesCopyItIsTheServersValueThatCounts()
    {
        var options = new InterposerOptions(Values(("OsVersion.Version", "WindowsXP")), null, Values(("OsVersion.Version", "WindowsXP")), true);

        options.ChangesConfig(bundled: false).ShouldBeFalse();
        options.ChangesConfig(bundled: true).ShouldBeTrue();
    }

    [Fact]
    public void ThePlayersValuesAreReadFromTheManifest()
    {
        var manifest = new ManifestGame
        {
            Redistributables = [new ManifestRedistributable { Id = InterposerOptions.RedistributableId, Options = Values(("Logging.Files", "true")) }],
            RedistributableOptions = new() { [InterposerOptions.RedistributableId] = Values(("Logging.Files", "false")) },
        };

        var options = InterposerOptions.For(manifest, new InterposerSettings { Options = Values(("Logging.Registry", "true")) });

        options.HasRedistributable.ShouldBeTrue();
        options.Get("Logging.Files").ShouldBe("false");
        options.Get("Logging.Registry").ShouldBe("true");
    }

    [Fact]
    public void AGameWithoutTheRedistributableHasNoServerValues()
    {
        var options = InterposerOptions.For(new ManifestGame(), null);

        options.HasRedistributable.ShouldBeFalse();
        options.Server.ShouldBeEmpty();
    }
}
