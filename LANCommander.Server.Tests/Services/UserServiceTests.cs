using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class UserServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task CreateAdminUserShouldWork()
    {
        var roleService = GetService<RoleService>();
        var userService = GetService<UserService>();

        roleService.AddAsync(new Role
        {
            Name = RoleService.AdministratorRoleName,
        });

        var user = await userService.AddAsync(new User
        {
            UserName = "admin",
        });

        await userService.ChangePassword(user.UserName, "Password1234");
        await userService.AddToRoleAsync(user.UserName, RoleService.AdministratorRoleName);

        user = await userService
            .Query(q =>
            {
                return q
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role);
            })
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == user.UserName.ToLower());
        
        user.UserName.ShouldBe("admin");
        user.NormalizedUserName.ShouldBe("ADMIN");
        user.Roles.ShouldContain(r => r.Name == RoleService.AdministratorRoleName);
    }
}