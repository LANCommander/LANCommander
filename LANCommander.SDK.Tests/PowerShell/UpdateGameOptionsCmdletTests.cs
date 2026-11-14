using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Extensions;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.PowerShell;

public class UpdateGameOptionsCmdletTests : IDisposable
{
    private const string Schema = """
        Options:
          Fullscreen:
            Type: bool
            Default: true
          Language:
            Type: choice
            Default: en
            Choices:
              - en
              - de
          Video:
            DisplayName: Video
            Options:
              Width:
                Type: int
                Default: 1024
              Height:
                Type: int
                Default: 768
          Maps:
            Type: list
            ItemType: string
          Bots:
            Type: list
            Fields:
              Name:
                Type: string
              Skill:
                Type: int
                Default: 3
        """;

    private readonly Guid _id = Guid.NewGuid();
    private readonly string _installDirectory;

    public UpdateGameOptionsCmdletTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), "lc-update-game-options-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_installDirectory);

        ManifestHelper.Write(new ManifestGame
        {
            Id = _id,
            Title = "Test Game",
            OptionSchema = Schema,
            Options = new Dictionary<string, string> { ["Language"] = "de" },
        }, _installDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_installDirectory, recursive: true);
        }
        catch
        {
            // Best effort
        }
    }

    private (List<ErrorRecord> Errors, List<WarningRecord> Warnings) Run(string script)
    {
        var sessionState = InitialSessionState.CreateDefault();

        sessionState.AddCustomCmdlets();

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);

        runspace.Open();
        runspace.SessionStateProxy.SetVariable("InstallDirectory", _installDirectory);
        runspace.SessionStateProxy.SetVariable("GameId", _id);

        using var shell = System.Management.Automation.PowerShell.Create();

        shell.Runspace = runspace;
        shell.AddScript(script);
        shell.Invoke();

        return (shell.Streams.Error.ToList(), shell.Streams.Warning.ToList());
    }

    private Dictionary<string, string> StoredOptions()
        => ManifestHelper.Read<ManifestGame>(_installDirectory, _id).Options ?? new();

    [Theory]
    [InlineData("Fullscreen", "$false", "false")]
    [InlineData("Fullscreen", "'True'", "true")]
    [InlineData("Video.Width", "1920", "1920")]
    [InlineData("Video.Width", "'2560'", "2560")]
    [InlineData("Language", "'en'", "en")]
    [InlineData("Maps", "@('dm1', 'dm2')", "[\"dm1\",\"dm2\"]")]
    [InlineData("Maps", "'dm1'", "[\"dm1\"]")]
    [InlineData("Bots", "@(@{ Name = 'Ranger'; Skill = 5 }, @{ Name = 'Sarge' })", "[{\"Name\":\"Ranger\",\"Skill\":\"5\"},{\"Name\":\"Sarge\",\"Skill\":\"3\"}]")]
    public void Single_StoresTheCanonicalStringForm(string name, string value, string expected)
    {
        var (errors, _) = Run($"Update-GameOptions -Path $InstallDirectory -Id $GameId -Name '{name}' -Value {value}");

        Assert.Empty(errors);
        Assert.Equal(expected, StoredOptions()[name]);

        // Other stored values are left alone
        if (name != "Language")
            Assert.Equal("de", StoredOptions()["Language"]);
    }

    [Fact]
    public void Bulk_RoundTripsFromGetGameOptions()
    {
        var (errors, _) = Run("""
            $options = Get-GameOptions -Path $InstallDirectory -Id $GameId
            $options.Video.Width = 1920
            $options.Fullscreen = $false
            Update-GameOptions -Path $InstallDirectory -Id $GameId -Options $options
            """);

        Assert.Empty(errors);

        var stored = StoredOptions();

        Assert.Equal("1920", stored["Video.Width"]);
        Assert.Equal("768", stored["Video.Height"]);
        Assert.Equal("false", stored["Fullscreen"]);
        Assert.Equal("de", stored["Language"]);
    }

    [Fact]
    public void Bulk_AcceptsNestedAndDotNotationHashtables()
    {
        var (errors, _) = Run("""
            Update-GameOptions -Path $InstallDirectory -Id $GameId -Options @{
                Video = @{ Height = 1080 }
                'Video.Width' = 1920
                Bots = @(@{ Name = 'Ranger'; Skill = 1 })
            }
            """);

        Assert.Empty(errors);

        var stored = StoredOptions();

        Assert.Equal("1080", stored["Video.Height"]);
        Assert.Equal("1920", stored["Video.Width"]);
        Assert.Equal("[{\"Name\":\"Ranger\",\"Skill\":\"1\"}]", stored["Bots"]);
    }

    [Fact]
    public void UnknownOption_ReportsAnError_AndStillWritesTheRest()
    {
        var (errors, _) = Run("Update-GameOptions -Path $InstallDirectory -Id $GameId -Options @{ Nope = 1; Fullscreen = $false }");

        Assert.Contains(errors, e => e.FullyQualifiedErrorId.StartsWith("UnknownOption"));
        Assert.Equal("false", StoredOptions()["Fullscreen"]);
        Assert.False(StoredOptions().ContainsKey("Nope"));
    }

    [Fact]
    public void InvalidValue_ReportsAnError_AndLeavesTheOptionAlone()
    {
        var (errors, _) = Run("Update-GameOptions -Path $InstallDirectory -Id $GameId -Name 'Video.Width' -Value 'wide'");

        Assert.Contains(errors, e => e.FullyQualifiedErrorId.StartsWith("InvalidOptionValue"));
        Assert.False(StoredOptions().ContainsKey("Video.Width"));
    }

    [Fact]
    public void ValueOutsideTheChoices_WarnsButIsStored()
    {
        var (errors, warnings) = Run("Update-GameOptions -Path $InstallDirectory -Id $GameId -Name 'Language' -Value 'fr'");

        Assert.Empty(errors);
        Assert.NotEmpty(warnings);
        Assert.Equal("fr", StoredOptions()["Language"]);
    }

    [Fact]
    public void Null_RemovesTheStoredValue()
    {
        var (errors, _) = Run("Update-GameOptions -Path $InstallDirectory -Id $GameId -Name 'Language' -Value $null");

        Assert.Empty(errors);
        Assert.False(StoredOptions().ContainsKey("Language"));
    }

    [Fact]
    public void MissingManifest_ReportsAnError()
    {
        var (errors, _) = Run($"Update-GameOptions -Path $InstallDirectory -Id '{Guid.NewGuid()}' -Name 'Fullscreen' -Value $true");

        Assert.Contains(errors, e => e.FullyQualifiedErrorId.StartsWith("ManifestNotFound"));
    }
}
