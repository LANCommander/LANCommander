using LANCommander.Server.Endpoints;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;
using Xunit.Abstractions;

namespace LANCommander.Server.UnitTests.Endpoints;

public partial class DownloadEndpointsTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task LauncherReturnsNotFoundIfNoReleases()
    {
        using var serviceProvider = new ServiceCollection()
            .AddLogging((builder) => builder.AddXUnit(outputHelper))
            .BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<UpdateService>>();

        var settings = new LANCommanderSettings();

        var githubClient = new Mock<IGitHubClient>();

        githubClient.Setup(client => client.Repository.Release.GetAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([]);

        var fileSystem = new MockFileSystem();

        var updateService = new UpdateService(
            logger,
            null!,
            null!,
            null!,
            githubClient.Object,
            fileSystem.Path);

        var result = await DownloadEndpoints.Launcher(
            updateService,
            settings,
            fileSystem.File,
            fileSystem.Path);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task LauncherReturnsRedirectResultWhenAssetFound()
    {
        using var serviceProvider = new ServiceCollection()
            .AddLogging((builder) => builder.AddXUnit(outputHelper))
            .BuildServiceProvider();
        var settings = new LANCommanderSettings();
        var logger = serviceProvider.GetRequiredService<ILogger<UpdateService>>();
        var githubClient = new Mock<IGitHubClient>();

        var downloadUrl = "https://browserDownloadUrl";

        Semver.SemVersion version = UpdateService.GetCurrentVersion();
        var release = new Release(
            "https://mock",
            "https://mock/html",
            "https://mock/assets",
            "https://mock/upload",
            1,
            "1",
            $"v{version}",
            "sha",
            "name",
            "body",
            false,
            false,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            new Author(),
            "https://mock/tarball",
            "https://mock/zip",
            [new ReleaseAsset("", 1, "", $"LANCommander.Launcher-Windows-x64-v{version}.zip", "", "", "", 1, 1, DateTimeOffset.Now, DateTimeOffset.Now, downloadUrl, new Author())]
            );
        githubClient.Setup(client => client.Repository.Release.GetAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([release]);
        var fileSystem = new MockFileSystem();
        var updateService = new UpdateService(
            logger,
            null!,
            null!,
            null!,
            githubClient.Object,
            fileSystem.Path);
        var result = await DownloadEndpoints.Launcher(
            updateService,
            settings,
            fileSystem.File,
            fileSystem.Path);
        Assert.IsType<RedirectHttpResult>(result);
        Assert.Equal(downloadUrl, ((RedirectHttpResult)result).Url);
    }

    [Fact]
    public async Task LauncherReturnsRedirectResultWhenNoAssetFound()
    {
        using var serviceProvider = new ServiceCollection()
            .AddLogging((builder) => builder.AddXUnit(outputHelper))
            .BuildServiceProvider();
        var settings = new LANCommanderSettings();
        var logger = serviceProvider.GetRequiredService<ILogger<UpdateService>>();
        var githubClient = new Mock<IGitHubClient>();

        var downloadUrl = "https://mock/html";

        Semver.SemVersion version = UpdateService.GetCurrentVersion();
        var release = new Release(
            "https://mock",
            downloadUrl,
            "https://mock/assets",
            "https://mock/upload",
            1,
            "1",
            $"v{version}",
            "sha",
            "name",
            "body",
            false,
            false,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            new Author(),
            "https://mock/tarball",
            "https://mock/zip",
            []
            );
        githubClient.Setup(client => client.Repository.Release.GetAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([release]);
        var fileSystem = new MockFileSystem();
        var updateService = new UpdateService(
            logger,
            null!,
            null!,
            null!,
            githubClient.Object,
            fileSystem.Path);
        var result = await DownloadEndpoints.Launcher(
            updateService,
            settings,
            fileSystem.File,
            fileSystem.Path);
        Assert.IsType<RedirectHttpResult>(result);
        Assert.Equal(downloadUrl, ((RedirectHttpResult)result).Url);
    }

    [Fact]
    public async Task LauncherWithExistingFileReturnsIt()
    {
        using var serviceProvider = new ServiceCollection()
            .AddLogging((builder) => builder.AddXUnit(outputHelper))
            .BuildServiceProvider();
        var settings = new LANCommanderSettings();
        var logger = serviceProvider.GetRequiredService<ILogger<UpdateService>>();
        var githubClient = new Mock<IGitHubClient>();
        var fileSystem = new MockFileSystem();
        var updateService = new UpdateService(
            logger,
            null!,
            null!,
            null!,
            githubClient.Object,
            fileSystem.Path);
        var version = UpdateService.GetCurrentVersion();
        var fileName = $"LANCommander.Launcher-Windows-x64-v{version}.zip";
        var zipPath = fileSystem.Path.Combine(settings.Launcher.StoragePath, fileName);
        fileSystem.AddFile(zipPath, new MockFileData("content"));
        var result = await DownloadEndpoints.Launcher(
            updateService,
            settings,
            fileSystem.File,
            fileSystem.Path);
        var typedResult = Assert.IsType<FileStreamHttpResult>(result);
        Assert.Equal("application/octet-stream", typedResult.ContentType);
        Assert.Equal(fileName, typedResult.FileDownloadName);
    }
}
