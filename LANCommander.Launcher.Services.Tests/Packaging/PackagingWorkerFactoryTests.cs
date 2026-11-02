using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Worker discovery has to survive being launched in more than one way.
/// </summary>
public class PackagingWorkerFactoryTests
{
    [Theory]
    [InlineData(ProcessArchitecture.X64, "x64")]
    [InlineData(ProcessArchitecture.X86, "x86")]
    public void LooksUnderTheApplicationDirectory(ProcessArchitecture architecture, string directoryName)
    {
        // The regression: resolution used only Environment.ProcessPath, which is dotnet.exe when
        // the launcher runs through the shared host. Discovery then pointed at the SDK install
        // and the packaging entry point silently never appeared.
        var candidates = PackagingWorkerFactory.GetCandidateWorkerPaths(architecture).ToList();

        candidates.ShouldContain(Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Libraries",
            "Packaging",
            directoryName,
            PackagingWorkerFactory.WorkerExecutableName));
    }

    [Fact]
    public void AlsoLooksBesideTheRunningExecutable()
    {
        // A published launcher runs from its own apphost, where this is the right answer.
        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);

        if (string.IsNullOrEmpty(processDirectory))
            return;

        var candidates = PackagingWorkerFactory.GetCandidateWorkerPaths(ProcessArchitecture.X64).ToList();

        var expected = Path.Combine(
            processDirectory,
            LauncherLibraries.DataDirectoryName,
            LauncherLibraries.LibrariesDirectoryName,
            LauncherLibraries.PackagingDirectoryName,
            "x64",
            PackagingWorkerFactory.WorkerExecutableName);

        // Only when the two directories actually differ; otherwise the app-base candidate covers it.
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(processDirectory),
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            candidates.ShouldContain(expected);
        }
    }

    [Fact]
    public void DoesNotRepeatADirectory()
    {
        var candidates = PackagingWorkerFactory.GetCandidateWorkerPaths(ProcessArchitecture.X64).ToList();

        candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(candidates.Count);
    }

    [Fact]
    public void OffersNoPathForAnArchitectureWithNoWorker()
    {
        // There is no ARM64 Interposer build, so no worker exists to look for.
        PackagingWorkerFactory.GetCandidateWorkerPaths(ProcessArchitecture.Arm64).ShouldBeEmpty();
        PackagingWorkerFactory.GetCandidateWorkerPaths(ProcessArchitecture.Unknown).ShouldBeEmpty();
    }

    [Fact]
    public void ResolvesNothingWhenNoWorkerIsDeployed()
    {
        // The test host has no Data\Libraries\Packaging directory, so this exercises the not-found path.
        PackagingWorkerFactory.ResolveWorkerPath(ProcessArchitecture.Arm64).ShouldBeNull();
        PackagingWorkerFactory.ResolveWorker(ProcessArchitecture.Arm64).ShouldBeNull();
    }

    [Fact]
    public void OnlyTheLauncherStartsItselfAsAWorker()
    {
        // Any other host using the factory (this test runner included) would otherwise start
        // itself with worker arguments it does not understand, and wait for a handshake that
        // never comes.
        var previous = PackagingWorkerFactory.LauncherHostsWorker;

        try
        {
            PackagingWorkerFactory.LauncherHostsWorker = false;

            PackagingWorkerFactory.ResolveWorker(ProcessArchitectureReader.Current)?.IsLauncher.ShouldNotBe(true);

            PackagingWorkerFactory.LauncherHostsWorker = true;

            var own = PackagingWorkerFactory.ResolveWorker(ProcessArchitectureReader.Current);

            if (LauncherLibraries.GetArchitectureDirectoryName(ProcessArchitectureReader.Current) == null)
            {
                own.ShouldBeNull();
                return;
            }

            own.ShouldNotBeNull();
            own.IsLauncher.ShouldBeTrue();
            own.Arguments.ShouldContain(PackagingWorkerFactory.LauncherWorkerArgument);
        }
        finally
        {
            PackagingWorkerFactory.LauncherHostsWorker = previous;
        }
    }
}
