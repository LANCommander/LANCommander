using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class UserServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task CreateAdminUserShouldWork()
    {
        var user = await EnsureAdminUserCreatedAsync();
        
        user.UserName.ShouldBe(TestConstants.AdminUserName);
        user.NormalizedUserName.ShouldBe(TestConstants.AdminUserName.ToUpper());
        user.Roles.ShouldContain(r => r.Name == RoleService.AdministratorRoleName);
    }

    [Fact]
    public async Task ChangePasswordShouldWork()
    {
        var user = await EnsureAdminUserCreatedAsync();
        var userService = GetService<UserService>();
        
        var result = await userService.ChangePassword(TestConstants.AdminUserName, TestConstants.AdminInitialPassword, TestConstants.AdminPassword);
        
        result.Succeeded.ShouldBeTrue();
        
        var validPassword = await userService.CheckPassword(TestConstants.AdminUserName, TestConstants.AdminPassword);
        
        validPassword.ShouldBeTrue();
    }
}