#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Debug-only fixture that simulates a file check so the Verify Files screen can be styled and its
/// states exercised without a damaged install. Set <c>LANCOMMANDER_FAKE_VERIFY</c> before starting a
/// Debug build:
/// <list type="bullet">
/// <item><c>1</c> / <c>problems</c> - a ~6 second check that turns up mismatched and missing files</item>
/// <item><c>clean</c> - the same check with nothing wrong</item>
/// <item><c>error</c> - the check fails part-way through</item>
/// </list>
/// Nothing is read from or written to disk and the server is never called; Repair only pretends.
/// </summary>
public partial class VerifyFilesViewModel
{
    public const string FixtureEnvironmentVariable = "LANCOMMANDER_FAKE_VERIFY";

    private const int FixtureFileCount = 1_607;
    private const int FixtureFilesPerTick = 12;
    private static readonly TimeSpan FixtureTick = TimeSpan.FromMilliseconds(45);

    private static string? FixtureMode =>
        Environment.GetEnvironmentVariable(FixtureEnvironmentVariable)?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "problems" => "problems",
            "clean" => "clean",
            "error" => "error",
            _ => null,
        };

    public static bool IsFixtureRequested => FixtureMode != null;

    private static readonly string[] FixtureFolders =
    [
        "Data",
        "Data\\Maps",
        "Data\\Textures",
        "Sound\\stream",
        "Sound\\terrain",
        "Campaign\\Media",
        "Games\\Forgotten Empires\\Script.AI",
        "Games\\Forgotten Empires\\Data",
    ];

    private static readonly string[] FixtureExtensions = [".dat", ".drs", ".mp3", ".wav", ".slp", ".ai", ".bin", ".cpx"];

    /// <summary>Index into the file list → problem, spread so problems keep arriving through the whole check.</summary>
    private static readonly Dictionary<int, (ArchiveValidationConflictType Type, string Path, long Length)> FixtureProblems = new()
    {
        [58]   = (ArchiveValidationConflictType.Mismatch, "language_x1_p1.dll", 1_474_560),
        [213]  = (ArchiveValidationConflictType.Missing,  "Data\\graphics.drs", 58_163_200),
        [388]  = (ArchiveValidationConflictType.Mismatch, "Data\\Maps\\Arabia.rms", 18_944),
        [602]  = (ArchiveValidationConflictType.Mismatch, "Sound\\stream\\xopen.mp3", 3_880_214),
        [811]  = (ArchiveValidationConflictType.Missing,  "Sound\\terrain\\river_ambience_loop_with_a_long_name_to_check_trimming.wav", 912_384),
        [1_044] = (ArchiveValidationConflictType.Mismatch, "Games\\Forgotten Empires\\Data\\empires2_x2_p1.dat", 12_502_766),
        [1_290] = (ArchiveValidationConflictType.Missing,  "Campaign\\Media\\intro.avi", 146_800_640),
        [1_512] = (ArchiveValidationConflictType.Mismatch, "age2_x1.exe", 2_695_168),
    };

    private async Task RunFixtureCheckAsync(IProgress<ArchiveValidationProgress> progress, CancellationToken cancellationToken)
    {
        var mode = FixtureMode;
        _logger.LogWarning("Verify fixture active ({Variable}={Mode}); no files are read", FixtureEnvironmentVariable, mode);

        var random = new Random(7);

        for (var i = 0; i < FixtureFileCount; i++)
        {
            if (i % FixtureFilesPerTick == 0)
                await Task.Delay(FixtureTick, cancellationToken);

            if (mode == "error" && i == FixtureFileCount * 2 / 5)
                throw new IOException(@"The process cannot access the file 'Data\sounds.drs' because it is being used by another process.");

            ArchiveValidationConflict? conflict = null;

            if (mode == "problems" && FixtureProblems.TryGetValue(i, out var problem))
            {
                var expected = (uint)random.NextInt64(0, uint.MaxValue);

                conflict = new ArchiveValidationConflict
                {
                    Type = problem.Type,
                    Name = Path.GetFileName(problem.Path),
                    FullName = problem.Path.Replace('\\', '/'),
                    Crc32 = expected,
                    LocalCrc32 = problem.Type == ArchiveValidationConflictType.Mismatch ? (uint)random.NextInt64(0, uint.MaxValue) : null,
                    Length = problem.Length,
                };
            }

            // Mirror the SDK: every conflict is reported, plain progress once per tick and on the last file.
            if (conflict != null || i % FixtureFilesPerTick == FixtureFilesPerTick - 1 || i == FixtureFileCount - 1)
            {
                progress.Report(new ArchiveValidationProgress
                {
                    CheckedFiles = i + 1,
                    TotalFiles = FixtureFileCount,
                    CurrentFile = conflict?.FullName ?? FixtureFileName(i),
                    Conflict = conflict,
                });
            }
        }

        // Progress<T> posts reports to the UI thread; yield so the last one lands before completion
        // clears the current file, as it does when the SDK's Task.Run returns.
        await Task.Yield();
    }

    private static Task RunFixtureRepairAsync() => Task.Delay(TimeSpan.FromSeconds(3));

    private static string FixtureFileName(int index)
    {
        var folder = FixtureFolders[index % FixtureFolders.Length];
        var extension = FixtureExtensions[(index / 3) % FixtureExtensions.Length];

        return $"{folder}/file_{index:D4}{extension}".Replace('\\', '/');
    }
}
#endif
