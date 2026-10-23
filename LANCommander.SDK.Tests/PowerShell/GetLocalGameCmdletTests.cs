using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell;

public class GetLocalGameCmdletTests
{
    private static readonly Guid InstalledId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AddonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NotInstalledId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly LocalGame[] Games =
    [
        new() { Id = InstalledId, Title = "Quake III Arena", Installed = true, InstallDirectory = @"C:\Games\Quake III Arena" },
        new() { Id = AddonId, Title = "Quake III Team Arena", Type = GameType.Expansion, BaseGameId = InstalledId, Installed = true, InstallDirectory = @"C:\Games\Quake III Arena" },
        new() { Id = NotInstalledId, Title = "Unreal Tournament", Installed = false },
    ];

    private static Task<string> RunAsync(string script)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();
        services.AddSingleton<ILocalLibraryProvider>(new FakeLocalLibraryProvider(Games));

        return new PowerShellScript(services.BuildServiceProvider(), ScriptType.Install, Options.Create(new SdkSettings()))
            .UseWorkingDirectory(AppContext.BaseDirectory)
            .UseInline(script)
            .ExecuteAsync<string>();
    }

    [Fact]
    public async Task ReturnsEveryGame_WithNoFilters()
    {
        var result = await RunAsync("$Return = (Get-LocalGame | ForEach-Object Title) -join '|'");

        Assert.Equal("Quake III Arena|Quake III Team Arena|Unreal Tournament", result);
    }

    [Fact]
    public async Task Installed_ExcludesGamesThatAreNotInstalled()
    {
        var result = await RunAsync("$Return = (Get-LocalGame -Installed | ForEach-Object Id) -join '|'");

        Assert.Equal($"{InstalledId}|{AddonId}", result);
    }

    [Fact]
    public async Task Title_MatchesWildcardsCaseInsensitively()
    {
        var result = await RunAsync("$Return = (Get-LocalGame -Title 'quake*arena' | ForEach-Object Title) -join '|'");

        Assert.Equal("Quake III Arena|Quake III Team Arena", result);
    }

    [Fact]
    public async Task Id_BindsFromPipelineByPropertyName()
    {
        var result = await RunAsync($"$Return = ([pscustomobject]@{{ Id = [guid]'{AddonId}' }} | Get-LocalGame).BaseGameId.ToString()");

        Assert.Equal(InstalledId.ToString(), result);
    }

    [Fact]
    public void ReportsAnError_WhenTheHostHasNoLocalLibrary()
    {
        var sessionState = InitialSessionState.CreateDefault();

        sessionState.AddCustomCmdlets();

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);

        runspace.Open();

        using var shell = System.Management.Automation.PowerShell.Create();

        shell.Runspace = runspace;
        shell.AddScript("Get-LocalGame");

        var output = shell.Invoke();

        Assert.Empty(output);
        Assert.Contains(shell.Streams.Error, e => e.FullyQualifiedErrorId.StartsWith("GetLocalGameError"));
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }

    private sealed class FakeLocalLibraryProvider(IReadOnlyList<LocalGame> games) : ILocalLibraryProvider
    {
        public Task<IReadOnlyList<LocalGame>> GetGamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(games);
    }
}
