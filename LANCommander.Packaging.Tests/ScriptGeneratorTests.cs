using LANCommander.Packaging.Changes;
using LANCommander.Packaging.Lcx;
using LANCommander.Packaging.Models;
using LANCommander.SDK.Enums;
using Shouldly;

namespace LANCommander.Packaging.Tests;

public class ScriptGeneratorTests
{
    [Fact]
    public void GeneratesNothingWithoutRegistryChangesOrOptions()
    {
        ScriptGenerator.Generate(new PackageDefinition()).ShouldBeEmpty();
    }

    [Fact]
    public void GeneratesInstallAndUninstallForRegistryChanges()
    {
        var package = PackageWith(new RegistryChange
        {
            Verb = "REG WRITE",
            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Example",
            ValueName = "InstallPath",
        });

        var scripts = ScriptGenerator.Generate(package);

        scripts.Count.ShouldBe(2);
        scripts.ShouldContain(s => s.Type == ScriptType.Install);
        scripts.ShouldContain(s => s.Type == ScriptType.Uninstall);
    }

    [Fact]
    public void GeneratesInstallOnlyForGameSpyPatch()
    {
        var package = new PackageDefinition { PatchGameSpy = true };

        var scripts = ScriptGenerator.Generate(package);

        scripts.Count.ShouldBe(1);
        scripts[0].Type.ShouldBe(ScriptType.Install);
        scripts[0].Contents.ShouldContain("Edit-PatchGameSpy");
    }

    [Fact]
    public void MachineKeysRequireAdmin()
    {
        var package = PackageWith(new RegistryChange
        {
            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Example",
            ValueName = "InstallPath",
        });

        ScriptGenerator.Generate(package).ShouldAllBe(s => s.RequiresAdmin);
    }

    [Fact]
    public void UserKeysDoNotRequireAdmin()
    {
        var package = PackageWith(new RegistryChange
        {
            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Example",
            ValueName = "InstallPath",
        });

        ScriptGenerator.Generate(package).ShouldAllBe(s => !s.RequiresAdmin);
    }

    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Example", @"HKLM:\SOFTWARE\Example")]
    [InlineData(@"HKEY_CURRENT_USER\SOFTWARE\Example", @"HKCU:\SOFTWARE\Example")]
    [InlineData(@"HKEY_CLASSES_ROOT\.example", @"HKCR:\.example")]
    [InlineData(@"HKEY_USERS\S-1-5-21\Example", @"HKU:\S-1-5-21\Example")]
    [InlineData(@"HKLM\SOFTWARE\Example", @"HKLM:\SOFTWARE\Example")]
    [InlineData(@"HKCU\SOFTWARE\Example", @"HKCU:\SOFTWARE\Example")]
    public void ConvertsHivesToProviderPaths(string input, string expected)
    {
        ScriptGenerator.ConvertToPowerShellRegistryPath(input).ShouldBe(expected);
    }

    [Fact]
    public void RedirectsSoftwareKeysCapturedFromX86Processes()
    {
        // A 32-bit installer writing HKLM\SOFTWARE\Example physically writes to
        // HKLM\SOFTWARE\WOW6432Node\Example. A script that targets the 64-bit view instead
        // leaves the game unable to find its own settings.
        ScriptGenerator.ConvertToPowerShellRegistryPath(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", ProcessArchitecture.X86)
            .ShouldBe(@"HKLM:\SOFTWARE\WOW6432Node\Example");
    }

    [Fact]
    public void DoesNotRedirectTwiceWhenAlreadyRedirected()
    {
        ScriptGenerator.ConvertToPowerShellRegistryPath(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Example", ProcessArchitecture.X86)
            .ShouldBe(@"HKLM:\SOFTWARE\WOW6432Node\Example");
    }

    [Fact]
    public void DoesNotRedirectNonSoftwareKeys()
    {
        // Only SOFTWARE is redirected under WOW64.
        ScriptGenerator.ConvertToPowerShellRegistryPath(
                @"HKEY_LOCAL_MACHINE\SYSTEM\Example", ProcessArchitecture.X86)
            .ShouldBe(@"HKLM:\SYSTEM\Example");
    }

    [Fact]
    public void DoesNotRedirectKeysThatMerelyStartWithSoftware()
    {
        ScriptGenerator.ConvertToPowerShellRegistryPath(
                @"HKEY_LOCAL_MACHINE\SOFTWAREX\Example", ProcessArchitecture.X86)
            .ShouldBe(@"HKLM:\SOFTWAREX\Example");
    }

    [Fact]
    public void DoesNotRedirectX64Captures()
    {
        ScriptGenerator.ConvertToPowerShellRegistryPath(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Example", ProcessArchitecture.X64)
            .ShouldBe(@"HKLM:\SOFTWARE\Example");
    }

    [Fact]
    public void KeyOnlyChangesCreateTheKeyWithoutSettingValues()
    {
        var package = PackageWith(new RegistryChange
        {
            Verb = "REG CREATE",
            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Example",
            ValueName = string.Empty,
        });

        var install = ScriptGenerator.Generate(package).Single(s => s.Type == ScriptType.Install);

        install.Contents.ShouldContain("New-Item");
        install.Contents.ShouldNotContain("Set-ItemProperty");
    }

    [Fact]
    public void SameKeyCapturedFromBothArchitecturesProducesBothPaths()
    {
        var package = PackageWith(
            new RegistryChange
            {
                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Example",
                ValueName = "A",
                SourceArchitecture = ProcessArchitecture.X86,
            },
            new RegistryChange
            {
                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Example",
                ValueName = "B",
                SourceArchitecture = ProcessArchitecture.X64,
            });

        var install = ScriptGenerator.Generate(package).Single(s => s.Type == ScriptType.Install);

        install.Contents.ShouldContain(@"HKLM:\SOFTWARE\WOW6432Node\Example");
        install.Contents.ShouldContain(@"HKLM:\SOFTWARE\Example");
    }

    private static PackageDefinition PackageWith(params RegistryChange[] changes) =>
        new() { SelectedRegistryEntries = [.. changes] };
}
