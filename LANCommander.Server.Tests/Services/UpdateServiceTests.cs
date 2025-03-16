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
    [Fact]
    public async Task GetLauncherArtifactsFromGitHub()
    {
        VersionProviderMock.SetVersion("1.0.0");
        
        var updateService = GetService<UpdateService>();

        var artifacts = await updateService.GetLauncherArtifactsFromGitHubAsync().ToListAsync();

        List<(LauncherArchitecture arch, LauncherPlatform platform, string name)> expecteds = new()
        {
            (LauncherArchitecture.arm64, LauncherPlatform.Linux, "LANCommander.Launcher-Linux-arm64-v1.0.0.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.Linux, "LANCommander.Launcher-Linux-x64-v1.0.0.zip"),
            (LauncherArchitecture.arm64, LauncherPlatform.macOS, "LANCommander.Launcher-macOS-arm64-v1.0.0.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.macOS, "LANCommander.Launcher-macOS-x64-v1.0.0.zip"),
            (LauncherArchitecture.arm64, LauncherPlatform.Windows, "LANCommander.Launcher-Windows-arm64-v1.0.0.zip"),
            (LauncherArchitecture.x64, LauncherPlatform.Windows, "LANCommander.Launcher-Windows-x64-v1.0.0.zip"),
        };
        
        artifacts.Count.ShouldBe(expecteds.Count);

        foreach (var expected in expecteds)
        {
            artifacts.ShouldContain(a => a.Architecture == expected.arch && a.Platform == expected.platform && a.Name == expected.name);
        } 
    }
}