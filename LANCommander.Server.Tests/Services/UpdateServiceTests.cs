using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Tests.Mocks;
using Moq;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class UpdateServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.0.0")]
    [InlineData("3.0.0")]
    [InlineData("4.0.34")]
    [InlineData("5.54.3")]
    public async Task GetLauncherArtifactsFromGitHub(string version)
    {
        VersionProviderMock.SetVersion(version);
        
        var updateService = GetService<UpdateService>();

        var artifacts = await updateService.GetLauncherArtifactsFromGitHubAsync().ToListAsync();

        List<(LauncherArchitecture arch, LauncherPlatform platform, string name)> expecteds = new()
        {
            (LauncherArchitecture.arm64, LauncherPlatform.Linux, $"LANCommander.Launcher-Linux-arm64-v{version}.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.Linux, $"LANCommander.Launcher-Linux-x64-v{version}.zip"),
            (LauncherArchitecture.arm64, LauncherPlatform.macOS, $"LANCommander.Launcher-macOS-arm64-v{version}.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.macOS, $"LANCommander.Launcher-macOS-x64-v{version}.zip"),
            (LauncherArchitecture.arm64, LauncherPlatform.Windows, $"LANCommander.Launcher-Windows-arm64-v{version}.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.Windows, $"LANCommander.Launcher-Windows-x64-v{version}.zip"),
        };
        
        artifacts.Count.ShouldBe(expecteds.Count);

        foreach (var expected in expecteds)
        {
            artifacts.ShouldContain(a => a.Architecture == expected.arch && a.Platform == expected.platform && a.Name == expected.name);
        }
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", ReleaseChannel.Stable)]
    [InlineData("1.0.0", "1.2.0", ReleaseChannel.Prerelease)]
    [InlineData("1.0.0", "1.1.4-nightly.20250314", ReleaseChannel.Nightly)]
    public async Task GetLauncherArtifactsFromGitHubAsync(string currentVersion, string latestVersion, ReleaseChannel releaseChannel)
    {
        VersionProviderMock.SetVersion(currentVersion);
        GitHubServiceMockFactory.OverrideVersion(latestVersion);
        
        var updateService = GetService<UpdateService>();

        var artifacts = await updateService.GetLauncherArtifactsFromGitHubAsync().ToListAsync();
        
        LauncherPlatform[] platforms = [LauncherPlatform.Windows, LauncherPlatform.Linux, LauncherPlatform.macOS];
        LauncherArchitecture[] archtectures = [LauncherArchitecture.x64, LauncherArchitecture.arm64];
        
        artifacts.ShouldNotBeEmpty();
        artifacts.Count.ShouldBe(platforms.Length * archtectures.Length);
        
        foreach (var platform in platforms)
        foreach (var architecture in archtectures)
        {
            var expectedArtifact = artifacts.SingleOrDefault(a => a.Platform == platform && a.Architecture == architecture);
            
            expectedArtifact.ShouldNotBeNull();
            expectedArtifact.Name.ShouldBe($"LANCommander.Launcher-{platform}-{architecture}-v{currentVersion}.zip");
            expectedArtifact.Url.ShouldBe($"https://github.com/LANCommander/LANCommander/releases/download/v{currentVersion}/LANCommander.Launcher-{platform}-{architecture}-v{currentVersion}.zip");
        }
    }
}