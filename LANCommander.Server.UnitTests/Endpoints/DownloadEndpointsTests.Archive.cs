using LANCommander.Server.Endpoints;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.UnitTests.Endpoints;

public partial class DownloadEndpointsTests
{
    [Fact(Skip = "Need to be able to mock the DbContext down the stack.")]
    public async Task ArchiveNotFound()
    {
        using var serviceProvider = new ServiceCollection()
            .AddLogging((builder) => builder.AddXUnit(outputHelper))
            .BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ArchiveService>>();
        var archiveService = new ArchiveService(
            logger,
            null!,
            null!,
            null!);
        var result = await DownloadEndpoints.Archive(
            Guid.NewGuid(),
            archiveService,
            new LANCommanderSettings(),
            new MockFileSystem().Path,
            new MockFileSystem().File);
        Assert.IsType<NotFound>(result);
    }
}
