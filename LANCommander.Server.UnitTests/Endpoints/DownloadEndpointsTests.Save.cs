using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace LANCommander.Server.UnitTests.Endpoints;

public partial class DownloadEndpointsTests
{
    [Fact]
    public async Task SaveCalledWithNoUserReturnsUnauthorised()
    {
        var mockFileSystem = new MockFileSystem();

        var httpContext = new DefaultHttpContext();

        var result = await DownloadEndpoints.Save(
            Guid.NewGuid(),
            new GameSaveService(
                null!,
                null!,
                null!,
                mockFileSystem.Path),
            mockFileSystem.File,
            httpContext);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact(Skip = "Need to be able to mock the DbContext down the stack.")]
    public async Task SaveForDifferentUserNotAccessible()
    {
        var mockFileSystem = new MockFileSystem();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "user")
        ]));

        var result = await DownloadEndpoints.Save(
            Guid.NewGuid(),
            new GameSaveService(
                null!,
                null!,
                null!,
                mockFileSystem.Path),
            mockFileSystem.File,
            httpContext);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact(Skip = "Need to be able to mock the DbContext down the stack.")]
    public async Task SaveForSameUserIsAccessible()
    {
        var mockFileSystem = new MockFileSystem();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "user")
        ]));
        var result = await DownloadEndpoints.Save(
            Guid.NewGuid(),
            new GameSaveService(
                null!,
                null!,
                null!,
                mockFileSystem.Path),
            mockFileSystem.File,
            httpContext);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact(Skip = "Need to be able to mock the DbContext down the stack.")]
    public async Task SaveIsAccessibleToAdmin()
    {
        var mockFileSystem = new MockFileSystem();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Administrator")
        ]));
        var result = await DownloadEndpoints.Save(
            Guid.NewGuid(),
            new GameSaveService(
                null!,
                null!,
                null!,
                mockFileSystem.Path),
            mockFileSystem.File,
            httpContext);
        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
