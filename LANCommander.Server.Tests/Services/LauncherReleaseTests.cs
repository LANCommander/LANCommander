using LANCommander.SDK.Updating;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class LauncherReleaseTests
{
    [Theory]
    [InlineData("LANCommander.Launcher-Windows-x64-v2.3.0.zip", "Windows", "x64", "2.3.0", UpdatePackageKind.Archive)]
    [InlineData("LANCommander.Launcher-Linux-arm64-v2.3.0-rc1.AppImage", "Linux", "arm64", "2.3.0-rc1", UpdatePackageKind.AppImage)]
    [InlineData("LANCommander.Launcher-macOS-arm64-v2.3.0-nightly.20261120.zip", "macOS", "arm64", "2.3.0-nightly.20261120", UpdatePackageKind.Archive)]
    public void ParsesReleaseFileNames(string fileName, string platform, string architecture, string version, UpdatePackageKind packageKind)
    {
        var release = UpdateService.ParseLauncherRelease(fileName);

        release.ShouldNotBeNull();
        release.Platform.ShouldBe(platform);
        release.Architecture.ShouldBe(architecture);
        release.Version.ToString().ShouldBe(version);
        release.PackageKind.ShouldBe(packageKind);
    }

    [Theory]
    [InlineData("LANCommander.Launcher-2.3.0-Windows-x64-Setup.exe")]
    [InlineData("LANCommander.Server-Windows-x64-v2.3.0.zip")]
    [InlineData("LANCommander.Launcher-Windows-x64-v2.3.0.tar.gz")]
    public void IgnoresOtherFiles(string fileName)
    {
        UpdateService.ParseLauncherRelease(fileName).ShouldBeNull();
    }
}
