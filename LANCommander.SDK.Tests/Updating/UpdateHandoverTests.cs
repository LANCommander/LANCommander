using System.Diagnostics;
using LANCommander.SDK.Updating;

namespace LANCommander.SDK.Tests.Updating;

public sealed class UpdateHandoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LANCommander.UpdateHandoverTests", Guid.NewGuid().ToString("N"));
    private readonly UpdateJournal _journal;

    public UpdateHandoverTests()
    {
        Directory.CreateDirectory(_root);

        _journal = UpdateJournal.Create(Path.Combine(_root, "Updates"), "2.0.0",
            new UpdateInstallation(_root, Path.Combine(_root, "App.exe"), UpdatePackageKind.Archive, IsBundle: false));

        _journal.State = UpdateState.Applied;
        _journal.Save();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public async Task Supervise_Succeeds_WhenTheUpdatedProcessConfirms()
    {
        using var process = StartShell(exitCode: 0, seconds: 30);

        // Stands in for the updated process reporting a healthy start
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await SelfUpdater.ConfirmStartupAsync(_journal.Directory);
        });

        try
        {
            Assert.True(await UpdateHandover.SuperviseAsync(_journal, process, TimeSpan.FromSeconds(20)));
        }
        finally
        {
            process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task Supervise_Fails_WhenTheUpdatedProcessExitsWithoutConfirming()
    {
        using var process = StartShell(exitCode: 3, seconds: 0);

        Assert.False(await UpdateHandover.SuperviseAsync(_journal, process, TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public async Task Supervise_StopsTheUpdatedProcess_WhenItNeverConfirms()
    {
        using var process = StartShell(exitCode: 0, seconds: 60);

        Assert.False(await UpdateHandover.SuperviseAsync(_journal, process, TimeSpan.FromMilliseconds(500)));
        Assert.True(process.HasExited);
    }

    [Fact]
    public void RunFileOperation_IgnoresOrdinaryArguments()
    {
        Assert.Null(UpdateHandover.RunFileOperation(["--big-screen"]));
    }

    [Fact]
    public void RunFileOperation_RollsBackTheGivenJournal()
    {
        Assert.Equal(0, UpdateHandover.RunFileOperation([UpdateHandover.RollbackArgument, _journal.Directory]));
        Assert.Equal(UpdateState.RolledBack, UpdateJournal.Load(_journal.Directory)!.State);
    }

    private static Process StartShell(int exitCode, int seconds)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c \"ping -n {seconds + 1} 127.0.0.1 >nul & exit {exitCode}\"")
            : new ProcessStartInfo("/bin/sh", $"-c \"sleep {seconds}; exit {exitCode}\"");

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        return Process.Start(startInfo)!;
    }
}
