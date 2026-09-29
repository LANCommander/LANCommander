// Every registry call in this file is reached only from a fact gated on Windows by
// WindowsFact / ElevatedWindowsFact / NonElevatedWindowsFact, or from cleanup guarded by
// TestHostElevation.IsWindows.
#pragma warning disable CA1416

using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell;

/// <summary>
/// Covers the elevated-script path end to end. Two things have to hold for a game's
/// <c>#Requires -RunAsAdministrator</c> install script to work:
///
/// 1. The SDK recognizes the requires-directive and flags the script <see cref="PowerShellScript.RunAsAdmin"/>,
///    which is the signal the launcher's elevation interceptor keys off of.
/// 2. Once the script actually runs in an elevated process, it can perform privileged work — writing
///    to HKLM being the canonical case (game installs registering install paths, version keys, etc.).
///
/// Each registry scenario is written once and run against two hives. The HKCU twin runs on every
/// Windows host and proves the script body, the variable plumbing and the assertions themselves are
/// sound; the HKLM twin runs only on an elevated host and is therefore a clean test of the privilege
/// alone. A negative control on a non-elevated host proves HKLM writes are genuinely privileged, so
/// the elevated twins cannot pass vacuously.
/// </summary>
public class ElevatedScriptExecutionTests : IDisposable
{
    private const string TestKeyRoot = @"SOFTWARE\LANCommander\Tests";

    private readonly string _workingDirectory;

    /// <summary>Hive-relative path of this instance's scratch key, unique per test instance.</summary>
    private readonly string _keySubPath;

    public ElevatedScriptExecutionTests()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), $"lc-ps-elevated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);

        _keySubPath = $@"{TestKeyRoot}\{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
            Directory.Delete(_workingDirectory, true);

        if (!TestHostElevation.IsWindows)
            return;

        DeleteTestKey(Registry.CurrentUser);
        DeleteTestKey(Registry.LocalMachine);
    }

    private string ProviderPath(RegistryKey hive) =>
        $@"{(hive == Registry.LocalMachine ? "HKLM" : "HKCU")}:\{_keySubPath}";

    private static PowerShellScript CreateScript(ScriptType type = ScriptType.Install)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();

        var provider = services.BuildServiceProvider();

        return new PowerShellScript(provider, type, Options.Create(new SdkSettings()));
    }

    #region Requires-admin detection

    [Fact]
    public void UseInline_WithRequiresRunAsAdministrator_FlagsScriptAsAdmin()
    {
        var script = CreateScript().UseInline("#Requires -RunAsAdministrator\r\nWrite-Host 'hello'");

        Assert.True(script.RunAsAdmin);
    }

    [Theory]
    [InlineData("#Requires Admin")]
    [InlineData("# Requires Admin")]
    [InlineData("#RequiresAdmin")]
    public void UseInline_WithRequiresAdminShorthand_FlagsScriptAsAdmin(string directive)
    {
        var script = CreateScript().UseInline($"{directive}\r\nWrite-Host 'hello'");

        Assert.True(script.RunAsAdmin);
    }

    [Fact]
    public void UseInline_WithoutRequiresDirective_DoesNotFlagScriptAsAdmin()
    {
        var script = CreateScript().UseInline("Write-Host 'hello'");

        Assert.False(script.RunAsAdmin);
    }

    [Fact]
    public void UseFile_WithRequiresRunAsAdministrator_FlagsScriptAsAdmin()
    {
        var scriptPath = Path.Combine(_workingDirectory, "install.ps1");

        File.WriteAllText(scriptPath, "#Requires -RunAsAdministrator\r\nWrite-Host 'hello'");

        var script = CreateScript().UseFile(scriptPath);

        Assert.True(script.RunAsAdmin);
    }

    [Fact]
    public void AsAdmin_IsStickyAcrossUseInline_ForScriptsWithoutADirective()
    {
        // A caller that has already decided a script needs elevation must not have that decision
        // undone by loading contents that carry no requires-directive.
        var script = CreateScript().AsAdmin().UseInline("Write-Host 'hello'");

        Assert.True(script.RunAsAdmin);
    }

    /// <summary>
    /// PowerShell honors <c>#Requires</c> on any line, so the SDK must too. A script that opens with a
    /// header comment or a blank line is the common real-world shape, and missing the directive there
    /// means the launcher runs it unelevated and it fails at its first privileged call.
    /// </summary>
    [Theory]
    [InlineData("# Installs the game\r\n#Requires -RunAsAdministrator\r\nWrite-Host 'hello'")]
    [InlineData("\r\n\r\n#Requires -RunAsAdministrator\r\nWrite-Host 'hello'")]
    [InlineData("<#\r\n  Banner\r\n#>\r\n#Requires -RunAsAdministrator\r\nWrite-Host 'hello'")]
    [InlineData("# Installs the game\n#Requires -RunAsAdministrator\nWrite-Host 'hello'")]
    public void UseInline_WithRequiresDirectiveBelowFirstLine_FlagsScriptAsAdmin(string contents)
    {
        var script = CreateScript().UseInline(contents);

        Assert.True(script.RunAsAdmin);
    }

    [Theory]
    [InlineData("    #Requires -RunAsAdministrator")]
    [InlineData("\t#Requires -RunAsAdministrator")]
    public void UseInline_WithIndentedRequiresDirective_FlagsScriptAsAdmin(string directive)
    {
        var script = CreateScript().UseInline($"{directive}\r\nWrite-Host 'hello'");

        Assert.True(script.RunAsAdmin);
    }

    /// <summary>
    /// PowerShell's <c>#Requires</c> is case-insensitive, and the directive may carry further
    /// parameters on the same line — neither form may slip past the elevation gate.
    /// </summary>
    [Theory]
    [InlineData("#requires -runasadministrator")]
    [InlineData("#Requires -RunAsAdministrator -Version 5.1")]
    public void UseInline_WithRequiresDirectiveVariants_FlagsScriptAsAdmin(string directive)
    {
        var script = CreateScript().UseInline($"{directive}\r\nWrite-Host 'hello'");

        Assert.True(script.RunAsAdmin);
    }

    /// <summary>
    /// The directive must be a comment at the start of a line. A mention inside a string literal or
    /// trailing another statement is not a directive and must not silently escalate the script.
    /// </summary>
    [Theory]
    [InlineData("Write-Host '#Requires -RunAsAdministrator'")]
    [InlineData("Write-Host 'hi' #Requires -RunAsAdministrator")]
    public void UseInline_WithRequiresTextMidLine_DoesNotFlagScriptAsAdmin(string contents)
    {
        var script = CreateScript().UseInline(contents);

        Assert.False(script.RunAsAdmin);
    }

    #endregion

    #region Writing values

    [ElevatedWindowsFact]
    public Task ElevatedScript_CanWriteValuesToHklm() => AssertCanWriteValues(Registry.LocalMachine);

    [WindowsFact]
    public Task Script_CanWriteValuesToHkcu() => AssertCanWriteValues(Registry.CurrentUser);

    private async Task AssertCanWriteValues(RegistryKey hive)
    {
        const string expectedInstallPath = @"C:\Games\LANCommander\TestGame";

        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("TestKeyPath", ProviderPath(hive))
            .AddVariable("ExpectedInstallPath", expectedInstallPath)
            .UseInline("""
                       #Requires -RunAsAdministrator

                       New-Item -Path $TestKeyPath -Force -ErrorAction Stop | Out-Null
                       New-ItemProperty -Path $TestKeyPath -Name 'InstallPath' -Value $ExpectedInstallPath -PropertyType String -Force -ErrorAction Stop | Out-Null
                       New-ItemProperty -Path $TestKeyPath -Name 'Build' -Value 1234 -PropertyType DWord -Force -ErrorAction Stop | Out-Null

                       $Return = (Get-ItemProperty -Path $TestKeyPath).InstallPath
                       """);

        // The same directive the launcher elevates on is present, so this is the real script shape.
        Assert.True(script.RunAsAdmin);

        var returned = await script.ExecuteAsync<string>();

        // Verified out-of-band through the plain .NET registry API rather than trusting the script's
        // own read-back, so a no-op script cannot fake a pass.
        using var key = hive.OpenSubKey(_keySubPath);

        Assert.NotNull(key);
        Assert.Equal(expectedInstallPath, key!.GetValue("InstallPath"));
        Assert.Equal(1234, key.GetValue("Build"));
        Assert.Equal(RegistryValueKind.DWord, key.GetValueKind("Build"));

        // And the script's own view of the write agrees.
        Assert.Equal(expectedInstallPath, returned);
    }

    #endregion

    #region Creating nested subkeys

    [ElevatedWindowsFact]
    public Task ElevatedScript_CanCreateNestedHklmSubkeys() => AssertCanCreateNestedSubkeys(Registry.LocalMachine);

    [WindowsFact]
    public Task Script_CanCreateNestedHkcuSubkeys() => AssertCanCreateNestedSubkeys(Registry.CurrentUser);

    private async Task AssertCanCreateNestedSubkeys(RegistryKey hive)
    {
        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("TestKeyPath", ProviderPath(hive))
            .UseInline("""
                       #Requires -RunAsAdministrator

                       New-Item -Path "$TestKeyPath\Publisher\Title" -Force -ErrorAction Stop | Out-Null
                       Set-ItemProperty -Path "$TestKeyPath\Publisher\Title" -Name 'Version' -Value '1.2.3' -ErrorAction Stop
                       """);

        await script.ExecuteAsync<object>();

        using var key = hive.OpenSubKey($@"{_keySubPath}\Publisher\Title");

        Assert.NotNull(key);
        Assert.Equal("1.2.3", key!.GetValue("Version"));
    }

    #endregion

    #region Updating and removing existing values

    [ElevatedWindowsFact]
    public Task ElevatedScript_CanUpdateAndDeleteExistingHklmValues() => AssertCanUpdateAndDeleteValues(Registry.LocalMachine);

    [WindowsFact]
    public Task Script_CanUpdateAndDeleteExistingHkcuValues() => AssertCanUpdateAndDeleteValues(Registry.CurrentUser);

    private async Task AssertCanUpdateAndDeleteValues(RegistryKey hive)
    {
        // Seed out-of-band so the script is exercised against a key it did not create itself.
        using (var seed = hive.CreateSubKey(_keySubPath))
        {
            seed.SetValue("InstallPath", @"C:\Old\Path");
            seed.SetValue("Stale", "remove-me");
        }

        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("TestKeyPath", ProviderPath(hive))
            .UseInline("""
                       #Requires -RunAsAdministrator

                       Set-ItemProperty -Path $TestKeyPath -Name 'InstallPath' -Value 'C:\New\Path' -ErrorAction Stop
                       Remove-ItemProperty -Path $TestKeyPath -Name 'Stale' -ErrorAction Stop
                       """);

        await script.ExecuteAsync<object>();

        using var key = hive.OpenSubKey(_keySubPath);

        Assert.NotNull(key);
        Assert.Equal(@"C:\New\Path", key!.GetValue("InstallPath"));
        Assert.Null(key.GetValue("Stale"));
    }

    #endregion

    #region Removing subkeys

    [ElevatedWindowsFact]
    public Task ElevatedScript_CanRemoveHklmSubkey() => AssertCanRemoveSubkey(Registry.LocalMachine);

    [WindowsFact]
    public Task Script_CanRemoveHkcuSubkey() => AssertCanRemoveSubkey(Registry.CurrentUser);

    private async Task AssertCanRemoveSubkey(RegistryKey hive)
    {
        using (var seed = hive.CreateSubKey($@"{_keySubPath}\Doomed"))
        {
            seed.SetValue("Value", "present");
        }

        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("TestKeyPath", ProviderPath(hive))
            .UseInline("""
                       #Requires -RunAsAdministrator

                       Remove-Item -Path "$TestKeyPath\Doomed" -Recurse -Force -ErrorAction Stop
                       """);

        await script.ExecuteAsync<object>();

        using var key = hive.OpenSubKey($@"{_keySubPath}\Doomed");

        Assert.Null(key);
    }

    #endregion

    #region Negative control — HKLM writes really are privileged

    /// <summary>
    /// The counterpart to the elevated tests: from a non-elevated host the identical script must fail
    /// to create the key. Paired with the HKCU twins — which pass on the same non-elevated host — this
    /// pins the failure to the missing privilege rather than to a broken script or runspace, so the
    /// elevated HKLM tests are proving something real.
    /// </summary>
    [NonElevatedWindowsFact]
    public async Task NonElevatedScript_CannotWriteToHklm()
    {
        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("TestKeyPath", ProviderPath(Registry.LocalMachine))
            .UseInline("""
                       New-Item -Path $TestKeyPath -Force -ErrorAction Stop | Out-Null
                       Set-ItemProperty -Path $TestKeyPath -Name 'InstallPath' -Value 'C:\Games' -ErrorAction Stop
                       """);

        await script.ExecuteAsync<object>();

        using var key = Registry.LocalMachine.OpenSubKey(_keySubPath);

        Assert.Null(key);
    }

    #endregion

    private void DeleteTestKey(RegistryKey hive)
    {
        try
        {
            hive.DeleteSubKeyTree(_keySubPath, throwOnMissingSubKey: false);

            // Prune the shared parents so repeated runs don't leave an empty tree behind.
            using var root = hive.OpenSubKey(TestKeyRoot);

            if (root is { SubKeyCount: 0, ValueCount: 0 })
                hive.DeleteSubKeyTree(TestKeyRoot, throwOnMissingSubKey: false);
        }
        catch
        {
            // Not elevated, or the key was never created — nothing to clean up.
        }
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
