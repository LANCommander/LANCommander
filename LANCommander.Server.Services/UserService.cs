using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace LANCommander.Server.Services
{
    public class UserService : BaseDatabaseService<User>
    {
        private readonly IdentityContextFactory IdentityContextFactory;

        public UserService(
            ILogger<UserService> logger,
            Repository<User> repository,
            RoleService roleService,
            IdentityContextFactory identityContextFactory) : base(logger, repository)
        {
            IdentityContextFactory = identityContextFactory;
        }

        public async Task<User> Get(string userName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.FindByNameAsync(userName);
            }
        }

        public async Task<IEnumerable<Role>> GetRoles(string userName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                var roleNames = await identityContext.UserManager.GetRolesAsync(user);

                return identityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name));
            }
        }

        public async Task<IEnumerable<Role>> GetRoles(User user)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var roleNames = await identityContext.UserManager.GetRolesAsync(user);

                return identityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name));
            }
        }

        public async Task<bool> IsInRole(User user, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.IsInRoleAsync(user, roleName);
            }
        }

        public override async Task<User> Add(User user)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var result = await identityContext.UserManager.CreateAsync(user);

                if (result.Succeeded)
                    return await identityContext.UserManager.FindByNameAsync(user.UserName);
                else
                    return null;
            }
        }

        public async Task AddToRole(User user, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                await identityContext.UserManager.AddToRoleAsync(user, roleName);
            }
        }

        public async Task AddToRoles(User user, IEnumerable<string> roleNames)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                await identityContext.UserManager.AddToRolesAsync(user, roleNames);
            }
        }

        public async Task RemoveFromRole(User user, Role role)
        {
            await RemoveFromRole(user, role.Name);
        }

        public async Task RemoveFromRole(User user, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                await identityContext.UserManager.RemoveFromRoleAsync(user, roleName);
            }
        }

        public async Task<bool> CheckPassword(User user, string password)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.CheckPasswordAsync(user, password);
            }
        }

        public async Task<IdentityResult> ChangePassword(User user, string currentPassword, string newPassword)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var result = await identityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

                return result;
            }
        }

        public async Task<IdentityResult> ChangePassword(User user, string newPassword)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var token = await identityContext.UserManager.GeneratePasswordResetTokenAsync(user);

                return await identityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
            }
        }

        public async Task SignOut()
        {

        }
    }
}
