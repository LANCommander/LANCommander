using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using Moq;
using Octokit;
using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LANCommander.Server.Tests.Mocks;

public static class GitHubServiceMockFactory
{
    public static SemVersion Version { get; set; } = new SemVersion(1, 0, 0);
    
    public static List<(string AssetType, string Platform, string Architecture)> ReleaseMatrix = new()
    {
        ("Launcher", "Windows", "arm64"),
        ("Launcher", "Windows", "x64"),
        ("Launcher", "Linux", "arm64"),
        ("Launcher", "Linux", "x64"),
        ("Launcher", "macOS", "arm64"),
        ("Launcher", "macOS", "x64"),
        ("Server", "Windows", "arm64"),
        ("Server", "Windows", "x64"),
        ("Server", "Linux", "arm64"),
        ("Server", "Linux", "x64"),
        ("Server", "macOS", "arm64"),
        ("Server", "macOS", "x64"),
    };

    public static void OverrideVersion(string version)
    {
        Version = SemVersion.Parse(version);
    }

    public static Release CreateRelease(SemVersion version)
    {
        var assets = new ReadOnlyCollection<ReleaseAsset>(ReleaseMatrix.Select(r => CreateReleaseAsset(version, r.AssetType, r.Platform, r.Architecture)).ToList());

        return new Release("", "", "", "", 0, "", $"v{version}", "", $"v{version}", "", false, false, DateTimeOffset.MinValue, null, null, "", "", assets);
    }
    
    public static ReleaseAsset CreateReleaseAsset(SemVersion version, string assetType, string platform, string architecture)
    {
        var releaseAssetName = $"LANCommander.{assetType}-{platform}-{architecture}-v{version}.zip";

        return new ReleaseAsset("", 0, "", releaseAssetName, "", "", "", 0, 0, DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            $"https://github.com/LANCommander/LANCommander/releases/download/v{version}/{releaseAssetName}", null);
    }

    public static Artifact CreateArtifact(SemVersion version, string assetType, string platform, string architecture)
    {
        var artifactMock = new Mock<Artifact>();
        var artifactName = $"LANCommander.{assetType}-{platform}-{architecture}-v{version}.zip";
        
        artifactMock.SetupProperty(ar => ar.Name, artifactName);
        
        return artifactMock.Object;
    }
    
    public static IGitHubService Create()
    {
        var mock = new Mock<IGitHubService>();

        // Setup GetLatestVersionAsync
        mock.Setup(x => x.GetLatestVersionAsync(It.IsAny<ReleaseChannel>()))
            .ReturnsAsync((ReleaseChannel channel) =>
            {
                return Version;
            });

        // Setup GetReleaseAsync
        mock.Setup(x => x.GetReleaseAsync(It.IsAny<SemVersion>()))
            .ReturnsAsync((SemVersion version) =>
            {
                return CreateRelease(version);
            });

        // Setup GetReleasesAsync
        mock.Setup(x => x.GetReleasesAsync(It.IsAny<int>()))
            .ReturnsAsync((int count) =>
            {
                var releases = new List<Release>();
                
                for (int i = 0; i < count; i++)
                {
                    var version = Version.WithMinor(i);
                    
                    releases.Add(CreateRelease(version));
                }

                return releases;
            });

        // Setup GetNightlyArtifactsAsync
        mock.Setup(x => x.GetNightlyArtifactsAsync(It.IsAny<string>()))
            .ReturnsAsync((string versionOverride) => ReleaseMatrix.Select(rm => CreateArtifact(SemVersion.Parse(versionOverride), rm.AssetType, rm.Platform, rm.Architecture)));

        // Setup GetWorkflowArtifactsAsync
        mock.Setup(x => x.GetWorkflowArtifactsAsync(It.IsAny<long>()))
            .ReturnsAsync((long runId) => ReleaseMatrix.Select(rm => CreateArtifact(Version, rm.AssetType, rm.Platform, rm.Architecture)));

        return mock.Object;
    }
}