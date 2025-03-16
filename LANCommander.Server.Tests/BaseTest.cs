using System.Formats.Asn1;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Extensions.TestDependency;

namespace LANCommander.Server.Tests;

[Trait("xUnit", "Ordered")]
[TestCaseOrderer(DependencyOrderer.TypeName, DependencyOrderer.AssemblyName)]
public abstract class BaseTest : IClassFixture<ApplicationFixture>, IDisposable
{
    protected readonly SDK.Client Client = ApplicationFixture.Instance.Client;
    protected readonly IServiceProvider ServiceProvider;
    
    private AsyncServiceScope? _scope;
    
    public BaseTest(ApplicationFixture fixture)
    {
        _scope = ApplicationFixture.Instance.ServiceProvider.CreateAsyncScope();
        
        ServiceProvider = _scope?.ServiceProvider;
    }
    
    protected T GetService<T>() => ServiceProvider.GetService<T>();

    protected async Task<User> EnsureAdminUserCreatedAsync()
    {
        var roleService = GetService<RoleService>();
        var userService = GetService<UserService>();

        var user = await userService
            .Query(q =>
            {
                return q
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role);
            })
            .FirstOrDefaultAsync(u => u.UserName == TestConstants.AdminUserName);

        // Assume user already exists in correct role
        if (user != null)
            return user;

        roleService.AddAsync(new Role
        {
            Name = RoleService.AdministratorRoleName,
        });

        user = await userService.AddAsync(new User
        {
            UserName = TestConstants.AdminUserName,
        });

        await userService.ChangePassword(user.UserName, TestConstants.AdminInitialPassword);
        await userService.AddToRoleAsync(user.UserName, RoleService.AdministratorRoleName);

        user = await userService
            .Query(q =>
            {
                return q
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role);
            })
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == user.UserName.ToLower());

        return user;
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}