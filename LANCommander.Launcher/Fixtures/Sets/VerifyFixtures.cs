#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using ByteSizeLib;
using LANCommander.Launcher.ViewModels;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The Verify Files page through a check and a repair.</summary>
public static class VerifyFixtures
{
    private const int TotalFiles = 1_607;

    /// <summary>What a damaged Age of Empires II install turns up. CRCs are fixed so the text is too.</summary>
    private static readonly ArchiveValidationConflict[] Problems =
    [
        Mismatch("language_x1_p1.dll", 1_474_560, 0x8F3A21C4, 0x1B77E902),
        Missing(@"Data\graphics.drs", 58_163_200, 0x5D0E44A1),
        Mismatch(@"Data\Maps\Arabia.rms", 18_944, 0xC2F11E3B, 0x0A9D6F52),
        Mismatch(@"Sound\stream\xopen.mp3", 3_880_214, 0x7E6B90DD, 0xE4412C07),
        Missing(@"Sound\terrain\river_ambience_loop_with_a_long_name_to_check_trimming.wav", 912_384, 0x33A8C5F0),
        Mismatch(@"Games\Forgotten Empires\Data\empires2_x2_p1.dat", 12_502_766, 0x9C04B7E2, 0x61F3D8AA),
        Missing(@"Campaign\Media\intro.avi", 146_800_640, 0x0F5B2E97),
    ];

    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Verify("Verify.Checking", "A check partway through, problems already turning up", verify =>
        {
            verify.IsChecking = true;
            verify.StatusTitle = "Checking files against the installed archive";
            verify.CheckedFiles = 642;
            verify.TotalFiles = TotalFiles;
            verify.Progress = 642d / TotalFiles;
            verify.CurrentFile = @"Sound\stream\file_0642.mp3";

            AddProblems(verify, Problems.Take(3));
        }),

        Verify("Verify.Problems", "A finished check that found damaged and missing files", verify =>
        {
            Complete(verify, "Check complete");
            AddProblems(verify, Problems);
        }),

        Verify("Verify.Clean", "A finished check with nothing wrong", verify =>
            Complete(verify, "All files match the archive")),

        Verify("Verify.Error", "A check that failed partway through", verify =>
        {
            verify.StatusTitle = "Verification failed";
            verify.ErrorMessage = @"The process cannot access the file 'Data\sounds.drs' because it is being used by another process.";
            verify.CheckedFiles = 642;
            verify.TotalFiles = TotalFiles;
            verify.Progress = 642d / TotalFiles;

            AddProblems(verify, Problems.Take(3));
        }),

        Verify("Verify.Repaired", "Every problem downloaded again", verify =>
        {
            Complete(verify, $"Repaired {Problems.Length} files");
            AddProblems(verify, Problems);

            foreach (var problem in verify.Problems)
                problem.IsRepaired = true;

            verify.IsRepaired = true;
            verify.RedownloadSizeText = "0 B";
        }),
    ];

    private static ArchiveValidationConflict Mismatch(string path, long length, uint expected, uint actual) =>
        Conflict(ArchiveValidationConflictType.Mismatch, path, length, expected, actual);

    private static ArchiveValidationConflict Missing(string path, long length, uint expected) =>
        Conflict(ArchiveValidationConflictType.Missing, path, length, expected, null);

    private static ArchiveValidationConflict Conflict(ArchiveValidationConflictType type, string path, long length, uint expected, uint? actual) => new()
    {
        Type = type,
        Name = path[(path.LastIndexOf('\\') + 1)..],
        FullName = path.Replace('\\', '/'),
        Crc32 = expected,
        LocalCrc32 = actual,
        Length = length,
    };

    private static void Complete(VerifyFilesViewModel verify, string title)
    {
        verify.IsCheckComplete = true;
        verify.StatusTitle = title;
        verify.CheckedFiles = TotalFiles;
        verify.TotalFiles = TotalFiles;
        verify.Progress = 1;
        verify.CurrentFile = string.Empty;
    }

    private static void AddProblems(VerifyFilesViewModel verify, IEnumerable<ArchiveValidationConflict> problems)
    {
        foreach (var problem in problems)
            verify.Problems.Add(new VerifyProblemViewModel(problem));

        verify.MismatchCount = verify.Problems.Count(p => p.Conflict.Type == ArchiveValidationConflictType.Mismatch);
        verify.MissingCount = verify.Problems.Count(p => p.Conflict.Type == ArchiveValidationConflictType.Missing);
        verify.RedownloadSizeText = ByteSize.FromBytes(verify.Problems.Sum(p => p.Conflict.Length)).ToString("0.#");
    }

    private static ViewFixture Verify(string name, string description, Action<VerifyFilesViewModel> configure) =>
        new(name, description, context =>
        {
            var game = FixtureGames.AgeOfEmpires2;
            var verify = new VerifyFilesViewModel(context.Services, game.Id, game.Title, $@"C:\Games\{game.Title}");

            configure(verify);

            return context.ShellWindow(verify);
        });
}
#endif
