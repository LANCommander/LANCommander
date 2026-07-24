using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell;

public class PowerShellScriptExecutionTests : IDisposable
{
    private readonly string _workingDirectory;

    public PowerShellScriptExecutionTests()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), $"lc-ps-exec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
            Directory.Delete(_workingDirectory, true);
    }

    private static PowerShellScript CreateScript(ScriptType type = ScriptType.Install)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();

        var provider = services.BuildServiceProvider();

        return new PowerShellScript(provider, type, Options.Create(new SdkSettings()));
    }

    [Fact]
    public async Task ExecuteAsync_RunsUnsignedInlineScript_AndReturnsValue()
    {
        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .UseInline("$Return = 42");

        var result = await script.ExecuteAsync<int>();

        // Reaching a real returned value proves the runspace opened (ExecutionPolicy.Bypass applied on
        // Windows) and the script executed rather than being silently skipped.
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesScriptSideEffects_InWorkingDirectory()
    {
        var markerPath = Path.Combine(_workingDirectory, "marker.txt");

        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .UseInline("Set-Content -Path (Join-Path $WorkingDirectory 'marker.txt') -Value 'ran'");

        await script.ExecuteAsync<int>();

        Assert.True(File.Exists(markerPath), "The script's side effect did not run — the script was skipped.");
        Assert.Equal("ran", (await File.ReadAllTextAsync(markerPath)).Trim());
    }

    [Fact]
    public async Task ExecuteAsync_PassesVariablesIntoScript()
    {
        var script = CreateScript()
            .UseWorkingDirectory(_workingDirectory)
            .AddVariable("Multiplier", 7)
            .UseInline("$Return = $Multiplier * 6");

        var result = await script.ExecuteAsync<int>();

        Assert.Equal(42, result);
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
