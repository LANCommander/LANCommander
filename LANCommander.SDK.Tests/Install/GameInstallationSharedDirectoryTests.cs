using System.Reflection;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using SdkGame = LANCommander.SDK.Models.Game;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Simulates two independent users (separate authentication sessions / launcher databases)
/// installing the same game into an overlapping install root.
///
/// Scenario:
///   - User A installs "Quake" with install root {root}\Games, landing in {root}\Games\Quake.
///   - User B, on a completely separate GameClient session, chooses the same {root}\Games root.
///   - User B's install should:
///       1. Resolve to (detect) User A's existing installation directory.
///       2. Validate the files already present in that directory.
///
/// Both users are modelled as distinct <see cref="GameClient"/> instances. GameClient's
/// directory resolution (<see cref="GameClient.GetInstallDirectory"/>) and local file
/// verification are file-system only for a main game, so no server/API is required.
/// </summary>
public class GameInstallationSharedDirectoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sharedRoot;
    private readonly Guid _quakeId = Guid.NewGuid();

    // Files User A "installs" into the shared directory, relative to the install directory.
    private static readonly string[] GameFiles =
    {
        "quake.exe",
        "id1/pak0.pak",
        "id1/config.cfg",
    };

    // User A's session/launcher.
    private readonly GameClient _userAClient = CreateClient();

    // User B's session/launcher — a separate authentication session and launcher DB.
    private readonly GameClient _userBClient = CreateClient();

    // Resolved install directory produced by User A's install ({sharedRoot}\Quake).
    private readonly string _userAInstallDirectory;

    public GameInstallationSharedDirectoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-shared-install-tests-{Guid.NewGuid()}");
        // Stands in for "C:\Games" — the install root both users select.
        _sharedRoot = Path.Combine(_tempDir, "Games");
        Directory.CreateDirectory(_sharedRoot);

        // ── User A performs the initial install ──────────────────────────────
        _userAInstallDirectory = _userAClient.GetInstallDirectory(MakeQuake(), _sharedRoot)
            .GetAwaiter().GetResult();

        InstallGameFiles(_userAInstallDirectory);
        WriteFileList(_userAInstallDirectory, _quakeId, GameFiles);

        ManifestHelper.Write(
            new ManifestGame
            {
                Id = _quakeId,
                Title = "Quake",
                Type = GameType.MainGame,
                Version = "1.0.0",
            },
            _userAInstallDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserB_ResolvesToUserAExistingInstallDirectory()
    {
        // User A installed to {sharedRoot}\Quake.
        Assert.Equal(Path.Combine(_sharedRoot, "Quake"), _userAInstallDirectory);

        // User B, on a separate session, selects the same shared root and should resolve
        // to the exact same directory User A already installed into.
        var userBInstallDirectory = await _userBClient.GetInstallDirectory(MakeQuake(), _sharedRoot);

        Assert.Equal(_userAInstallDirectory, userBInstallDirectory);
        Assert.True(Directory.Exists(userBInstallDirectory));
    }

    [Fact]
    public async Task UserB_DetectsExistingInstallation_ViaManifest()
    {
        var userBInstallDirectory = await _userBClient.GetInstallDirectory(MakeQuake(), _sharedRoot);

        // The existing installation is detected through the manifest User A wrote.
        Assert.True(ManifestHelper.Exists(userBInstallDirectory, _quakeId));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserB_ValidatesExistingFiles_AllPresent()
    {
        var userBInstallDirectory = await _userBClient.GetInstallDirectory(MakeQuake(), _sharedRoot);

        var verified = await VerifyLocalFiles(_userBClient, userBInstallDirectory, _quakeId);

        // Every file User A installed is validated as present and can be skipped on re-download.
        Assert.Equal(GameFiles.Length, verified.Count);
        foreach (var file in GameFiles)
            Assert.Contains(file, verified);
    }

    [Fact]
    public async Task UserB_Validation_DetectsMissingFile()
    {
        var userBInstallDirectory = await _userBClient.GetInstallDirectory(MakeQuake(), _sharedRoot);

        // Simulate a partially-present/corrupted existing install by removing a file.
        File.Delete(Path.Combine(userBInstallDirectory, "id1/pak0.pak".Replace('/', Path.DirectorySeparatorChar)));

        var verified = await VerifyLocalFiles(_userBClient, userBInstallDirectory, _quakeId);

        // The missing file is not validated, so User B's install would re-fetch it.
        Assert.DoesNotContain("id1/pak0.pak", verified);
        Assert.Contains("quake.exe", verified);
        Assert.Equal(GameFiles.Length - 1, verified.Count);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private SdkGame MakeQuake() => new()
    {
        Id = _quakeId,
        Title = "Quake",
        Type = GameType.MainGame,
        BaseGameId = Guid.Empty,
    };

    /// <summary>
    /// Builds a GameClient whose dependencies are unused for main-game directory
    /// resolution and local file verification, so they are safe to leave null.
    /// </summary>
    private static GameClient CreateClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    private static void InstallGameFiles(string installDirectory)
    {
        foreach (var file in GameFiles)
        {
            var localPath = Path.Combine(installDirectory, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            File.WriteAllText(localPath, $"contents of {file}");
        }
    }

    /// <summary>
    /// Writes a FileList.txt matching the production format ("path | CRC32HEX")
    /// that GameClient produces after an extraction.
    /// </summary>
    private static void WriteFileList(string installDirectory, Guid gameId, IEnumerable<string> files)
    {
        var fileListPath = GameClient.GetMetadataFilePath(installDirectory, gameId, "FileList.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fileListPath)!);

        var builder = new StringBuilder();
        foreach (var file in files)
            builder.AppendLine($"{file} | DEADBEEF");

        File.WriteAllText(fileListPath, builder.ToString());
    }

    /// <summary>
    /// Invokes GameClient's private local-file verification (the VerifyFiles install task),
    /// which reads FileList.txt and returns entries confirmed present on disk.
    /// </summary>
    private static async Task<HashSet<string>> VerifyLocalFiles(GameClient client, string installDirectory, Guid gameId)
    {
        var method = typeof(GameClient).GetMethod(
            "VerifyLocalFilesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var task = (Task<HashSet<string>>)method!.Invoke(
            client,
            new object[] { installDirectory, gameId, CancellationToken.None })!;

        return await task;
    }
}
