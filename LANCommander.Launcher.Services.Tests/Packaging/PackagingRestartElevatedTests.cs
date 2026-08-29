using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Ipc;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// Covers what happens to the already-running installer when a capture is restarted elevated.
/// </summary>
public class PackagingRestartElevatedTests
{
    [Fact]
    public async Task RestartingElevatedTerminatesTheRunningInstaller()
    {
        // Without this the un-elevated installer keeps running, so the user ends up with two
        // copies of the same installer on screen writing to the same place.
        var (session, factory) = Build();

        await session.StartAsync(Options());

        var original = factory.Created.ToList();

        await session.RestartElevatedAsync();

        foreach (var worker in original)
        {
            worker.SentOfType<StopCommand>()
                .ShouldHaveSingleItem()
                .TerminateTargets.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task AnOrdinaryStopLeavesTheInstallerAlone()
    {
        // The user may be mid-install; killing their installer is worse than losing the tail
        // of a capture.
        var (session, factory) = Build();

        await session.StartAsync(Options());
        await session.StopAsync();

        foreach (var worker in factory.Created)
        {
            worker.SentOfType<StopCommand>()
                .ShouldHaveSingleItem()
                .TerminateTargets.ShouldBeFalse();
        }
    }

    private static (PackagingSessionService Session, FakePackagingWorkerFactory Factory) Build()
    {
        var factory = new FakePackagingWorkerFactory(
            ProcessArchitecture.X64, ProcessArchitecture.X86);

        return (new PackagingSessionService(factory, NullLogger<PackagingSessionService>.Instance), factory);
    }

    private static PackagingSessionOptions Options() =>
        new() { InstallerPath = @"C:\Installers\setup.exe" };
}
