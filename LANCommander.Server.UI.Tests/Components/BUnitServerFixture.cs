using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Shared fixture for bUnit component tests. Reuses the proven <see cref="UITestApplicationFactory"/>
/// to stand up the real server dependency-injection container backed by a file-based SQLite
/// database, seeds an admin user and a single test game, then exposes the real service provider so
/// bUnit can resolve the server's scoped services (GameService, AntDesign, etc.) while rendering
/// components in-process.
///
/// Unlike <see cref="ConfiguredServerFixture"/> this does NOT start Playwright — bUnit renders
/// components synchronously in-process and needs only the DI container and seeded data.
/// </summary>
public class BUnitServerFixture : IAsyncLifetime
{
    public UITestApplicationFactory Factory { get; private set; } = null!;

    /// <summary>
    /// ID of a game created via the service layer for edit component tests.
    /// </summary>
    public Guid TestGameId { get; private set; }
    public const string TestGameTitle = "Test Game";

    public async Task InitializeAsync()
    {
        Factory = new UITestApplicationFactory();
        // Trigger the factory to build the host and create the SQLite schema.
        _ = Factory.Services;

        using var scope = Factory.RealServices.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        await roleService.AddAsync(new Role { Name = RoleService.AdministratorRoleName });
        var user = await userService.AddAsync(new User { UserName = TestConstants.AdminUserName });
        await userService.ChangePassword(user.UserName, TestConstants.AdminPassword);
        await userService.AddToRoleAsync(user.UserName, RoleService.AdministratorRoleName);

        // Seed default storage locations so service initialization mirrors a real server.
        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

        var archivePath = Path.Combine(Path.GetTempPath(), "LANCommander_BUnit_Archives");
        Directory.CreateDirectory(archivePath);
        await storageLocationService.AddAsync(new StorageLocation
        {
            Path = archivePath,
            Type = StorageLocationType.Archive,
            Default = true
        });

        var savePath = Path.Combine(Path.GetTempPath(), "LANCommander_BUnit_Saves");
        Directory.CreateDirectory(savePath);
        await storageLocationService.AddAsync(new StorageLocation
        {
            Path = savePath,
            Type = StorageLocationType.Save,
            Default = true
        });

        var mediaPath = Path.Combine(Path.GetTempPath(), "LANCommander_BUnit_Media");
        Directory.CreateDirectory(mediaPath);
        await storageLocationService.AddAsync(new StorageLocation
        {
            Path = mediaPath,
            Type = StorageLocationType.Media,
            Default = true
        });

        // Seed a test game via the service layer for edit component tests.
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var game = await gameService.AddAsync(new Game
        {
            Title = TestGameTitle,
            Type = GameType.MainGame,
            Singleplayer = true
        });
        TestGameId = game.Id;

        // Mark the provider as configured so the app behaves as a set-up server.
        DatabaseContext.Provider = DatabaseProvider.SQLite;
    }

    public async Task DisposeAsync()
    {
        DatabaseContext.Provider = DatabaseProvider.Unknown;
        await Factory.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition that shares a single <see cref="BUnitServerFixture"/> across all
/// bUnit component test classes, keeping them isolated from the Playwright "Server" collection so
/// the static <see cref="DatabaseContext.Provider"/> is not contended.
/// </summary>
[CollectionDefinition("BUnit")]
public class BUnitCollection : ICollectionFixture<BUnitServerFixture>
{
}
