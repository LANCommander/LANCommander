using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using SdkSettings = LANCommander.SDK.Models.Settings;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Verifies the admin-elevation path for launcher scripts. When a script is flagged
/// <c>#Requires -RunAsAdministrator</c> and the launcher is not already elevated, the interceptor
/// must re-launch the launcher as a minimal elevated process, pass it every runtime parameter the
/// script needs, wait until that process exits, and only then report the script as handled. In every
/// other case (no admin required, already elevated, or a failure) it must fall through so the script
/// runs in-process.
/// </summary>
public class ElevatedScriptInterceptorTests
{
    private static PowerShellScript CreateScript(ScriptType type)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();

        var provider = services.BuildServiceProvider();

        return new PowerShellScript(provider, type, Options.Create(new SdkSettings()));
    }

    [Fact]
    public async Task NonAdminScript_ReturnsFalse_AndDoesNotLaunchElevatedProcess()
    {
        var processInfo = new FakeCurrentProcessInfo { IsElevated = false };
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.Install);
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");
        // Note: not calling AsAdmin() — script does not require elevation.

        var handled = await interceptor.ExecuteAsync(script);

        Assert.False(handled);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task AdminScript_WhenAlreadyElevated_ReturnsFalse_AndDoesNotLaunchElevatedProcess()
    {
        var processInfo = new FakeCurrentProcessInfo { IsElevated = true };
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.Install).AsAdmin();
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.False(handled);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task AdminScript_WhenNotElevated_LaunchesMinimalLauncherWithRunAsParametersAndWaits()
    {
        var gameId = Guid.NewGuid();
        var processInfo = new FakeCurrentProcessInfo
        {
            IsElevated = false,
            ExecutablePath = @"C:\LANCommander\LANCommander.Launcher.exe",
        };
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.Install).AsAdmin().UseWorkingDirectory("WorkDir");
        script.AddVariable("GameManifest", new ManifestGame { Id = gameId });
        script.AddVariable("InstallDirectory", "InstallDir");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.True(handled);
        Assert.Equal(1, launcher.LaunchCount);

        var request = Assert.Single(launcher.Requests);

        // Re-launches this same launcher executable as the elevated process.
        Assert.Equal(processInfo.ExecutablePath, request.FileName);
        // Preserves the working directory so the elevated script runs in the right place.
        Assert.Equal("WorkDir", request.WorkingDirectory);

        // Passes the RunScript verb plus every parameter the elevated process needs to run the script.
        Assert.Contains("RunScript", request.Arguments);
        Assert.Contains(gameId.ToString(), request.Arguments);
        Assert.Contains("InstallDir", request.Arguments);
        Assert.Contains(ScriptType.Install.ToString(), request.Arguments);

        // The interceptor must not report the script handled until the elevated process has exited.
        Assert.True(launcher.CompletedBeforeReturn);
    }

    [Fact]
    public async Task KeyChangeScript_ForwardsAllocatedKeyToElevatedProcess()
    {
        var processInfo = new FakeCurrentProcessInfo { IsElevated = false };
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.KeyChange).AsAdmin();
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");
        script.AddVariable("AllocatedKey", "KEY-12345");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.True(handled);
        var request = Assert.Single(launcher.Requests);
        Assert.Contains(ScriptType.KeyChange.ToString(), request.Arguments);
        Assert.Contains("KEY-12345", request.Arguments);
    }

    [Fact]
    public async Task NameChangeScript_ForwardsOldAndNewAliasesToElevatedProcess()
    {
        var processInfo = new FakeCurrentProcessInfo { IsElevated = false };
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.NameChange).AsAdmin();
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");
        script.AddVariable("OldPlayerAlias", "OldAlias");
        script.AddVariable("NewPlayerAlias", "NewAlias");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.True(handled);
        var request = Assert.Single(launcher.Requests);
        Assert.Contains(ScriptType.NameChange.ToString(), request.Arguments);
        Assert.Contains("OldAlias", request.Arguments);
        Assert.Contains("NewAlias", request.Arguments);
    }

    [Fact]
    public async Task WhenElevationCheckThrows_ReturnsFalse_SoScriptRunsInProcess()
    {
        var processInfo = new ThrowingCurrentProcessInfo();
        var launcher = new RecordingElevatedProcessLauncher();
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.Install).AsAdmin();
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.False(handled);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task WhenElevatedLaunchFails_ReturnsFalse_SoScriptRunsInProcess()
    {
        var processInfo = new FakeCurrentProcessInfo { IsElevated = false };
        var launcher = new RecordingElevatedProcessLauncher { ThrowOnLaunch = true };
        var interceptor = new ElevatedScriptInterceptor(processInfo, launcher);

        var script = CreateScript(ScriptType.Install).AsAdmin();
        script.AddVariable("GameManifest", new ManifestGame { Id = Guid.NewGuid() });
        script.AddVariable("InstallDirectory", "InstallDir");

        var handled = await interceptor.ExecuteAsync(script);

        Assert.False(handled);
    }

    private sealed class FakeCurrentProcessInfo : ICurrentProcessInfo
    {
        public string ExecutablePath { get; init; } = @"C:\LANCommander\LANCommander.Launcher.exe";
        public bool IsElevated { get; init; }
    }

    private sealed class ThrowingCurrentProcessInfo : ICurrentProcessInfo
    {
        public string ExecutablePath => throw new InvalidOperationException("path unavailable");
        public bool IsElevated => throw new InvalidOperationException("cannot determine elevation");
    }

    private sealed class RecordingElevatedProcessLauncher : IElevatedProcessLauncher
    {
        public List<ElevatedProcessRequest> Requests { get; } = new();
        public int LaunchCount => Requests.Count;
        public bool ThrowOnLaunch { get; init; }

        /// <summary>Set once the (awaited) launch has fully completed. Proves the caller waited.</summary>
        public bool CompletedBeforeReturn { get; private set; }

        public async Task LaunchAndWaitAsync(ElevatedProcessRequest request)
        {
            Requests.Add(request);

            if (ThrowOnLaunch)
                throw new InvalidOperationException("elevated launch failed");

            // Simulate the elevated process running for a moment; if the interceptor did not await
            // this, CompletedBeforeReturn would still be false when ExecuteAsync returns.
            await Task.Delay(20);

            CompletedBeforeReturn = true;
        }
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
