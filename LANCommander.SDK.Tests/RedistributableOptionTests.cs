using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestRedistributable = LANCommander.SDK.Models.Manifest.Redistributable;

namespace LANCommander.SDK.Tests;

/// <summary>
/// A compatibility shim's options resolve from its schema's defaults, then the server's values for the
/// game, then the action's overrides, then the player's own values, which the launcher writes into the
/// manifest. Windows only: the action runs cmd.exe.
/// </summary>
public class RedistributableOptionTests : IDisposable
{
    private readonly string _installDirectory = Directory.CreateTempSubdirectory("shim-options").FullName;

    public void Dispose() => Directory.Delete(_installDirectory, recursive: true);

    private static readonly Guid ShimId = Guid.NewGuid();

    private const string Schema = """
        Options:
          Shim:
            Options:
              Layer:
                Type: string
                IsEnvironmentVariable: true
                Default: "default"
              Untouched:
                Type: string
                IsEnvironmentVariable: true
                Default: "default"
        """;

    [Theory]
    [InlineData(false, false, false, "default")]
    [InlineData(true, false, false, "server")]
    [InlineData(true, true, false, "action")]
    [InlineData(true, true, true, "player")]
    [InlineData(false, false, true, "player")]
    public async Task ThePlayersValueWinsOverEveryOtherLayer(bool server, bool action, bool player, string expected)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var manifest = new ManifestGame
        {
            Id = Guid.NewGuid(),
            Title = "Shimmed Game",
            Redistributables =
            [
                new ManifestRedistributable
                {
                    Id = ShimId,
                    Name = "Shim",
                    OptionSchema = Schema,
                    Options = server ? new() { ["Shim.Layer"] = "server" } : new(),
                },
            ],
            RedistributableOptions = player ? new() { [ShimId] = new() { ["Shim.Layer"] = "player" } } : new(),
        };

        await ManifestHelper.WriteAsync(manifest, _installDirectory);

        var marker = Path.Combine(_installDirectory, "environment.txt");

        var gameAction = new ManifestAction
        {
            Name = "Play",
            Path = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c set > \"{marker}\"",
            IsPrimaryAction = true,
            OptionOverrides = action
                ? JsonSerializer.Serialize(new Dictionary<Guid, Dictionary<string, string>> { [ShimId] = new() { ["Shim.Layer"] = "action" } })
                : null,
        };

        using var context = new ProcessExecutionContext(NullLogger<ProcessExecutionContext>.Instance);

        await context.ExecuteGameActionAsync(_installDirectory, manifest.Id, gameAction);

        var environment = File.ReadAllText(marker);

        Assert.Contains($"Layer={expected}", environment);
        Assert.Contains("Untouched=default", environment);
    }

    /// <summary>
    /// A null from the player takes the option out altogether -- for an option armed by its key's
    /// presence, such as a device list, whose absence is the setting.
    /// </summary>
    [Fact]
    public async Task ThePlayersNullTakesTheOptionOut()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var manifest = new ManifestGame
        {
            Id = Guid.NewGuid(),
            Title = "Shimmed Game",
            Redistributables =
            [
                new ManifestRedistributable
                {
                    Id = ShimId,
                    Name = "Shim",
                    OptionSchema = Schema,
                    Options = new() { ["Shim.Layer"] = "server" },
                },
            ],
            RedistributableOptions = new() { [ShimId] = new() { ["Shim.Layer"] = null! } },
        };

        await ManifestHelper.WriteAsync(manifest, _installDirectory);

        var marker = Path.Combine(_installDirectory, "environment.txt");

        using var context = new ProcessExecutionContext(NullLogger<ProcessExecutionContext>.Instance);

        await context.ExecuteGameActionAsync(_installDirectory, manifest.Id, new ManifestAction
        {
            Name = "Play",
            Path = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c set > \"{marker}\"",
            IsPrimaryAction = true,
        });

        var environment = File.ReadAllText(marker);

        Assert.DoesNotContain("Layer=", environment.Replace("Untouched=", ""));
        Assert.Contains("Untouched=default", environment);
    }
}
