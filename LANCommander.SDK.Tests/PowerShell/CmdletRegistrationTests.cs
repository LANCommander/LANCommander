using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Extensions;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell;

/// <summary>
/// PowerShell constructs cmdlets itself through <see cref="Activator"/>, so a cmdlet declared with
/// constructor dependencies registers without complaint and only fails when a script actually calls
/// it: "Type '...' does not have a default constructor (Parameter 'type')". These tests catch that
/// at build time rather than when a game's install script runs.
/// </summary>
public class CmdletRegistrationTests
{
    private static IReadOnlyList<SessionStateCmdletEntry> GetRegisteredCmdlets()
    {
        var sessionState = InitialSessionState.Create();

        sessionState.AddCustomCmdlets();

        return sessionState.Commands.OfType<SessionStateCmdletEntry>().ToList();
    }

    [Fact]
    public void AddCustomCmdlets_RegistersTheGeneratedCmdletSet()
    {
        var cmdlets = GetRegisteredCmdlets();

        // Guards against the source generator silently producing nothing, which would make every
        // other assertion here vacuously pass.
        Assert.NotEmpty(cmdlets);

        var names = cmdlets.Select(c => c.Name).ToList();

        Assert.Contains("Get-UserCustomField", names);
        Assert.Contains("Update-UserCustomField", names);
        Assert.Contains("Out-PlayerAvatar", names);
    }

    [Fact]
    public void AddCustomCmdlets_EveryRegisteredCmdlet_CanBeConstructedByPowerShell()
    {
        var failures = new List<string>();

        foreach (var entry in GetRegisteredCmdlets())
        {
            var type = entry.ImplementingType;

            if (!typeof(Cmdlet).IsAssignableFrom(type))
            {
                failures.Add($"{entry.Name} ({type.FullName}) does not derive from Cmdlet.");
                continue;
            }

            var constructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (constructor is null)
            {
                var dependencies = type
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.ParameterType.Name);

                failures.Add(
                    $"{entry.Name} ({type.FullName}) has no public parameterless constructor. " +
                    $"PowerShell cannot inject [{string.Join(", ", dependencies)}] — resolve services from " +
                    $"{nameof(ScriptServicesProvider)} / SessionState instead.");

                continue;
            }

            try
            {
                Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                failures.Add($"{entry.Name} ({type.FullName}) threw when constructed: {ex.GetBaseException().Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("Get-UserCustomField -Name 'Steam.Id'")]
    [InlineData("Update-UserCustomField -Name 'Steam.Id' -Value '1234'")]
    [InlineData("Out-PlayerAvatar")]
    public void ProfileCmdlets_Invoke_WithoutConstructionFailure(string command)
    {
        var sessionState = InitialSessionState.CreateDefault();

        sessionState.AddCustomCmdlets();

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);

        runspace.Open();

        using var shell = System.Management.Automation.PowerShell.Create();

        shell.Runspace = runspace;
        shell.AddScript(command);
        shell.Invoke();

        var errors = shell.Streams.Error.Select(e => e.ToString() + " " + e.FullyQualifiedErrorId).ToList();

        // No ProfileClient is published into this bare runspace, so the cmdlet is expected to report a
        // missing service. What must not happen is PowerShell failing to construct the cmdlet at all.
        Assert.DoesNotContain(errors, e => e.Contains("default constructor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_PublishesProfileClientIntoSessionState()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();
        services.AddSingleton<ITokenProvider, FakeTokenProvider>();
        services.AddSingleton<IConnectionClient, FakeConnectionClient>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<ApiRequestFactory>();
        services.AddSingleton<ProfileClient>();

        var provider = services.BuildServiceProvider();

        var script = new PowerShellScript(provider, ScriptType.Install, Options.Create(new SdkSettings()))
            .UseWorkingDirectory(AppContext.BaseDirectory)
            .UseInline($"$Return = (Get-Variable -Name '{ScriptServicesProvider.ProfileClientKey}' -ValueOnly -ErrorAction SilentlyContinue).GetType().Name");

        var result = await script.ExecuteAsync<string>();

        // Without this the profile cmdlets construct fine but can never reach the server.
        Assert.Equal(nameof(ProfileClient), result);
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }

    private sealed class FakeTokenProvider : ITokenProvider
    {
        private AuthToken? _token;

        public void SetToken(AuthToken token) => _token = token;

        public AuthToken GetToken() => _token!;
    }

    private sealed class FakeConnectionClient : IConnectionClient
    {
        public event EventHandler? OnConnect;
        public event EventHandler? OnDisconnect;
        public event EventHandler? OnServerAddressChanged;
        public event EventHandler? OnOfflineModeEnabled;

        public bool IsConnected() => false;
        public bool IsConfigured() => false;
        public bool IsOfflineMode() => true;
        public bool HasServerAddress() => false;
        public Uri GetServerAddress() => new("http://localhost");

        public Task UpdateServerAddressAsync(string address) => Task.CompletedTask;
        public Task UpdateServerAddressAsync(Uri address) => Task.CompletedTask;

        public Task<bool> ConnectAsync() => Task.FromResult(false);
        public Task<bool> DisconnectAsync() => Task.FromResult(true);
        public Task EnableOfflineModeAsync() => Task.CompletedTask;
        public Task<bool> PingAsync(Uri? serverAddress = null) => Task.FromResult(false);
    }
}
