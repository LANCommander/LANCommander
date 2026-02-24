using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

public class InstallProgressTests
{
    [Fact]
    public void Progress_WhenBytesTransferredIsZeroAndTotalBytesIsPositive_ReturnsZero()
    {
        var progress = new InstallProgress
        {
            BytesTransferred = 0,
            TotalBytes = 1000
        };

        Assert.Equal(0f, progress.Progress);
    }

    [Fact]
    public void Progress_WhenBytesTransferredEqualsTotalBytes_ReturnsOne()
    {
        var progress = new InstallProgress
        {
            BytesTransferred = 500,
            TotalBytes = 500
        };

        Assert.Equal(1f, progress.Progress);
    }

    [Fact]
    public void Progress_WhenHalfTransferred_ReturnsPointFive()
    {
        var progress = new InstallProgress
        {
            BytesTransferred = 250,
            TotalBytes = 500
        };

        Assert.Equal(0.5f, progress.Progress);
    }

    [Fact]
    public void Progress_WhenTotalBytesIsZero_ReturnsNaN()
    {
        var progress = new InstallProgress
        {
            BytesTransferred = 0,
            TotalBytes = 0
        };

        Assert.True(float.IsNaN(progress.Progress));
    }

    [Theory]
    [InlineData(InstallStatus.Downloading)]
    [InlineData(InstallStatus.InstallingRedistributables)]
    [InlineData(InstallStatus.RunningScripts)]
    [InlineData(InstallStatus.Complete)]
    [InlineData(InstallStatus.Failed)]
    [InlineData(InstallStatus.Canceled)]
    public void Status_CanBeSetToAnyInstallStatus(InstallStatus status)
    {
        var progress = new InstallProgress { Status = status };

        Assert.Equal(status, progress.Status);
    }

    [Fact]
    public void Indeterminate_CanBeSet()
    {
        var progress = new InstallProgress { Indeterminate = true };

        Assert.True(progress.Indeterminate);
    }
}
