using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>Builds .lcx packages for import tests, laid out the way the exporter writes them.</summary>
internal static class ImportPackages
{
    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    public static byte[] ZipBytes(string content)
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("content.txt").Open());
            writer.Write(content);
        }

        return buffer.ToArray();
    }

    /// <summary>Set by every current exporter; without it a manifest is read as a legacy one.</summary>
    public static string ManifestVersion => VersionHelper.GetCurrentVersion().ToString();

    /// <summary>A game manifest with two actions, an archive, a save and two LAN modes.</summary>
    public static Manifest.Game Game() => new()
    {
        ManifestVersion = ManifestVersion,
        Id = Guid.NewGuid(),
        Title = $"Imported Game {Guid.NewGuid():N}",
        DirectoryName = "ImportedGame",
        Singleplayer = true,
        Type = GameType.MainGame,
        Actions =
        [
            new Manifest.Action { Name = "Play", Path = "game.exe", Type = ActionType.Primary, SortOrder = 0 },
            new Manifest.Action { Name = "Editor", Path = "editor.exe", Type = ActionType.Secondary, SortOrder = 1 },
        ],
        Archives =
        [
            new Manifest.Archive { Id = Guid.NewGuid(), Version = "1.0", ObjectKey = Guid.NewGuid().ToString() },
        ],
        Saves =
        [
            new Manifest.Save { Id = Guid.NewGuid(), User = TestConstants.AdminUserName },
        ],
        MultiplayerModes =
        [
            new Manifest.MultiplayerMode { Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Versus", MinPlayers = 2, MaxPlayers = 8 },
            new Manifest.MultiplayerMode { Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Co-op", MinPlayers = 2, MaxPlayers = 4 },
        ],
    };

    /// <summary>
    /// Writes the manifest plus a payload for each of its archives and saves. <paramref name="payload"/>
    /// replaces the bytes written for an entry, e.g. to corrupt one.
    /// </summary>
    public static async Task<string> WriteAsync(Manifest.Game manifest, Func<string, byte[]>? payload = null)
        => await WriteAsync(manifest,
            manifest.Archives.Select(a => $"Archives/{a.Id}")
                .Concat(manifest.Saves.Select(s => $"Saves/{s.Id}")),
            payload);

    public static async Task<string> WriteAsync(Manifest.Tool manifest)
        => await WriteAsync(manifest, manifest.Archives.Select(a => $"Archives/{a.Id}"));

    public static async Task<string> WriteAsync(Manifest.Server manifest)
        => await WriteAsync(manifest, []);

    public static async Task<string> WriteAsync(Manifest.Redistributable manifest)
        => await WriteAsync(manifest, manifest.Archives.Select(a => $"Archives/{a.Id}"));

    private static async Task<string> WriteAsync<T>(T manifest, IEnumerable<string> payloads, Func<string, byte[]>? payload = null)
    {
        var path = Path.Combine(TempDirectory(), "package.lcx");

        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        await using (var writer = new StreamWriter(zip.CreateEntry(ManifestHelper.ManifestFilename).Open(), Encoding.UTF8))
            await writer.WriteAsync(ManifestHelper.Serialize(manifest));

        foreach (var entryName in payloads)
            await using (var entry = zip.CreateEntry(entryName).Open())
                await entry.WriteAsync(payload?.Invoke(entryName) ?? ZipBytes(entryName));

        return path;
    }
}
