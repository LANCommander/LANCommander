using System.Runtime.InteropServices;
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
    [InlineData(ProcessArchitecture.X64, "win-x64")]
    [InlineData(ProcessArchitecture.X86, "win-x86")]
    public void LooksUnderTheApplicationDirectory(ProcessArchitecture architecture, string runtimeIdentifier)
    {
        // The regression: resolution used only Environment.ProcessPath, which is dotnet.exe when
        // the launcher runs through the shared host. Discovery then pointed at the SDK install
        // and the packaging entry point silently never appeared.
        var candidates = PackagingWorkerFactory.GetCandidateWorkerPaths(architecture).ToList();

        candidates.ShouldContain(p =>
            p.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase) &&
            p.Contains(runtimeIdentifier, StringComparison.OrdinalIgnoreCase) &&
            p.EndsWith(PackagingWorkerFactory.WorkerExecutableName, StringComparison.OrdinalIgnoreCase));
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
            PackagingWorkerFactory.WorkersDirectoryName,
            "win-x64",
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
        // The test host has no Packaging directory, so this exercises the not-found path.
        PackagingWorkerFactory.ResolveWorkerPath(ProcessArchitecture.Arm64).ShouldBeNull();
    }
}
