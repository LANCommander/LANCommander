#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using ByteSizeLib;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.ViewModels.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.Analysis;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>
/// The packaging wizard at each step, for an Unreal Tournament 2004 install that has already been
/// captured. Nothing is monitored, scanned, looked up or built; each step is filled directly.
/// </summary>
public static class PackagingFixtures
{
    private const string Installer = @"D:\Installers\UT2004\Setup.exe";
    private const string InstallDirectory = @"C:\Games\Unreal Tournament 2004";

    private static readonly string[] CaptureStartLog =
    [
        "Starting capture of Setup.exe...",
        "Monitoring Setup.exe [X86]",
        "Routing Setup.tmp [X86] to the X86 worker",
    ];

    private static readonly string[] CaptureSummaryLog =
    [
        "The installer exited.",
        "Captured 1243 file(s) and 37 registry change(s).",
        "Processes seen:",
        "  PID 4120  Setup.exe  [X86]  monitored",
        "  PID 4388  Setup.tmp  [X86]  monitored",
        "  PID 5012  DXSETUP.exe  [X86]  NOT monitored",
        "Top directories by captured file count:",
        $"  {812,6}  {InstallDirectory}\\Textures",
        $"  {301,6}  {InstallDirectory}\\Maps",
        $"  {130,6}  {InstallDirectory}\\System",
    ];

    /// <summary>A small slice of an install, relative to the install folder.</summary>
    private static readonly (string Path, long Size)[] Files =
    [
        ("System/UT2004.exe", 9_216_000),
        ("System/UCC.exe", 118_784),
        ("System/Setup.exe", 1_310_720),
        ("System/UT2004.ini", 41_216),
        ("System/Core.dll", 1_720_320),
        ("System/Engine.dll", 7_512_064),
        ("Maps/ONS-Torlan.ut2", 21_430_272),
        ("Maps/ONS-Primeval.ut2", 18_874_368),
        ("Maps/ONS-Torlan-LAN.ut2", 21_431_808),
        ("Maps/DM-Rankin.ut2", 9_437_184),
        ("Textures/ONSDeadVehicles-TX.utx", 12_582_912),
        ("Textures/UT2004Weapons.utx", 31_457_280),
        ("Sounds/WeaponSounds.uax", 26_214_400),
        ("Music/KR-UT2004-Menu.ogg", 4_194_304),
        ("Help/UT2004Logo.bmp", 262_144),
        ("Help/ReadMe.txt", 51_200),
    ];

    private static readonly DirectoryChange[] DirectoryChanges =
    [
        Change(DirectoryChangeKind.Added, @"Maps\ONS-Torlan-LAN.ut2", 21_431_808),
        Change(DirectoryChangeKind.Added, @"System\ServerActors.ini", 2_048),
        Change(DirectoryChangeKind.Added, @"System\Manifest.ini", 6_144),
        Change(DirectoryChangeKind.Modified, @"System\UT2004.ini", 41_216, 37_120),
        Change(DirectoryChangeKind.Modified, @"System\User.ini", 18_432, 18_432),
        Change(DirectoryChangeKind.Removed, @"Help\ReadMe-Patch3339.txt", 0, 12_288),
    ];

    private static readonly RegistryChange[] RegistryChanges =
    [
        Registry("REG CREATE", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Unreal Technology\Installed Apps\UT2004", "Folder"),
        Registry("REG CREATE", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Unreal Technology\Installed Apps\UT2004", "Version"),
        Registry("REG CREATE", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Unreal Technology\Installed Apps\UT2004", "CDKey"),
        Registry("REG WRITE", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\DirectX", "Version"),
        Registry("REG CREATE", @"HKEY_CURRENT_USER\Software\Unreal Technology\UT2004", ""),
    ];

    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Step<MonitorStepViewModel>("Packaging.Monitor", "Choosing an installer to monitor", _ => { }),

        Step<MonitorStepViewModel>("Packaging.Monitor.Capturing", "Watching an installer run", monitor =>
        {
            monitor.InstallerPath = Installer;
            monitor.IsMonitoring = true;
            monitor.CanGoNext = false;
            monitor.Status = "Complete the install, then stop monitoring.";
            monitor.FileCount = 1_243;
            monitor.RegistryCount = 37;
            monitor.ProcessCount = 3;
            Log(monitor, CaptureStartLog);
        }),

        Step<MonitorStepViewModel>("Packaging.Monitor.Finished", "An installer captured and summarized", monitor =>
        {
            monitor.InstallerPath = Installer;
            monitor.Status = "Capture finished. Continue to choose what goes into the package.";
            monitor.FileCount = 1_243;
            monitor.RegistryCount = 37;
            monitor.ProcessCount = 3;
            monitor.UninstrumentedProcessCount = 1;
            Log(monitor, [.. CaptureStartLog, .. CaptureSummaryLog]);
        }),

        Step<MonitorStepViewModel>("Packaging.Monitor.Elevation", "An installer that needs administrator rights", monitor =>
        {
            monitor.InstallerPath = Installer;
            monitor.NeedsElevation = true;
            monitor.ElevationMessage = "The installer requires administrator rights to be monitored.";
        }),

        Step<InstallDirectoryStepViewModel>("Packaging.InstallFolder", "The install folder, detected from the capture", step =>
        {
            step.InstallDirectory = InstallDirectory;
            step.DetectionSummary = "Detected from 1243 captured file(s).";
        }),

        Step<PostInstallStepViewModel>("Packaging.Customize", "Changes made by hand after the install", step =>
        {
            step.BaselineFileCount = 1_243;
            step.HasScanned = true;
            step.AddedCount = 3;
            step.ModifiedCount = 2;
            step.RemovedCount = 1;
            step.ScanSummary = "3 added, 2 changed, 1 removed.";

            foreach (var change in DirectoryChanges)
                step.Changes.Add(new DirectoryChangeItem(change));

            step.AdditionalInstallers.Add(@"D:\Installers\UT2004\ut2004-winpatch3369.exe");
            step.Status = "Patch capture finished. Run another, make more changes by hand, or continue.";
            step.FileCount = 212;
            step.RegistryCount = 1;
            step.ProcessCount = 1;
            Log(step, ["Starting capture of ut2004-winpatch3369.exe...", "The installer exited.", "Captured 212 file(s) and 1 registry change(s)."]);
        }),

        Step<FileSelectionStepViewModel>("Packaging.Files", "Choosing which captured files go into the package", step =>
        {
            var root = CheckableTreeNode.BuildFileTree(
                Files.Select(f => (FullPath(f.Path), f.Path)),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [FullPath("System/UT2004.ini")] = "changed",
                    [FullPath("Maps/ONS-Torlan-LAN.ut2")] = "added",
                },
                path => Files.First(f => FullPath(f.Path) == path).Size);

            root.Name = InstallDirectory;

            // Leave the help files out, so the tree shows unchecked and partly checked folders.
            root.Children.First(c => c.Name == "Help").IsChecked = false;

            step.Roots.Add(root);
            step.SweptFileCount = 17;
            step.PostInstallFileCount = 2;

            var selected = Files.Where(f => !f.Path.StartsWith("Help/", StringComparison.Ordinal)).ToList();
            step.Summary = $"{selected.Count} of {Files.Length} file(s) selected · {ByteSize.FromBytes(selected.Sum(f => f.Size)).ToString("0.##")}";
        }),

        Step<RegistrySelectionStepViewModel>("Packaging.Registry", "Choosing which registry values to recreate", step =>
            step.OnEnterAsync().GetAwaiter().GetResult()),

        Step<MetadataStepViewModel>("Packaging.Details", "The game's details, filled in", Details),

        Step<MetadataStepViewModel>("Packaging.Details.Lookup", "Looking the game up in a metadata provider", step =>
        {
            Details(step);

            step.Providers.Add("IGDB");
            step.Providers.Add("SteamGridDB");
            step.SelectProviderForFixture("IGDB");
            step.SearchQuery = "Unreal Tournament 2004";

            foreach (var (title, year) in new[] { ("Unreal Tournament 2004", 2004), ("Unreal Tournament 2004: Editor's Choice Edition", 2004), ("Unreal Tournament 2003", 2002), ("Unreal Tournament", 1999) })
            {
                step.SearchResults.Add(new MetadataSearchResult
                {
                    Id = FixtureGames.IdFor("metadata " + title).ToString(),
                    Data = new SDK.Models.Manifest.Game { Title = title, ReleasedOn = new DateTime(year, 3, 16) },
                });
            }

            step.SelectedResult = step.SearchResults[0];
            step.IsSearchOpen = true;
        }),

        Step<ActionStepViewModel>("Packaging.Actions", "How the launcher starts the game", step =>
        {
            step.SeedFilesForFixture(Files.Select(f => f.Path));

            step.AddActionForFixture("Play", "System/UT2004.exe", isPrimary: true);
            step.AddActionForFixture("Dedicated Server", "System/UCC.exe", "server ONS-Torlan?game=Onslaught.ONSOnslaughtGame");
            step.AddActionForFixture("Settings", "System/Setup.exe");
        }),

        Step<OutputStepViewModel>("Packaging.Finish", "Ready to build the package", Output),

        Step<OutputStepViewModel>("Packaging.Finish.Uploading", "Uploading the built package", step =>
        {
            Output(step);
            step.PublishToServer = true;
            step.IsBuilding = true;
            step.Progress = 42;
            step.Status = "Uploading... 42%";
        }),

        Step<OutputStepViewModel>("Packaging.Finish.Done", "The package built and saved", step =>
        {
            Output(step);
            step.IsComplete = true;
            step.Status = $"Saved to {step.OutputPath}.";
        }),

        Step<OutputStepViewModel>("Packaging.Finish.Failed", "Building the package failed", step =>
        {
            Output(step);
            step.ErrorMessage = "There is not enough space on the disk.";
            step.Status = "Packaging failed.";
        }),
    ];

    private static DirectoryChange Change(DirectoryChangeKind kind, string relativePath, long length, long previousLength = 0) => new()
    {
        Kind = kind,
        Path = $@"{InstallDirectory}\{relativePath}",
        RelativePath = relativePath,
        Length = length,
        PreviousLength = previousLength,
    };

    private static RegistryChange Registry(string verb, string key, string value) => new()
    {
        Verb = verb,
        KeyPath = key,
        ValueName = value,
        SourceArchitecture = ProcessArchitecture.X86,
        ProcessId = 4120,
    };

    private static string FullPath(string relativePath) => $@"{InstallDirectory}\{relativePath.Replace('/', '\\')}";

    private static void Log(CaptureStepViewModel step, IEnumerable<string> lines)
    {
        foreach (var line in lines)
            step.Log.Add(line);

        step.LogText = string.Join("\n", step.Log);
    }

    private static void Details(MetadataStepViewModel step)
    {
        step.GameTitle = FixtureGames.UnrealTournament2004.Title;
        step.SortTitle = "Unreal Tournament 2004";
        step.Version = "3369";
        step.ReleasedOn = new DateTimeOffset(2004, 3, 16, 0, 0, 0, TimeSpan.Zero);
        step.Singleplayer = true;
        step.Description = FixtureGames.UnrealTournament2004.Description;
        step.Notes = "Patched to 3369 with the Editor's Choice bonus pack.";
    }

    private static void Output(OutputStepViewModel step)
    {
        step.OutputPath = @"D:\Packages\Unreal Tournament 2004.lcx";
    }

    /// <summary>The wizard on step <typeparamref name="T"/>, with every step before it done.</summary>
    private static ViewFixture Step<T>(string name, string description, Action<T> configure) where T : PackagingStepViewModel =>
        new(name, description, context =>
        {
            var wizard = context.Shell.PackagingWizardViewModel;
            var package = wizard.Package;

            package.InstallerPath = Installer;
            package.InstallDirectory = InstallDirectory;
            package.RegistryChanges = [.. RegistryChanges];
            package.Manifest.Title = FixtureGames.UnrealTournament2004.Title;

            // What resetting the wizard does before it's shown.
            foreach (var step in wizard.Steps)
                step.CanGoNext = true;

            var current = wizard.Steps.OfType<T>().Single();

            configure(current);

            wizard.CurrentStep = current;

            return context.ShellWindow(wizard);
        });
}
#endif
