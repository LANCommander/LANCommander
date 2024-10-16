using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.Services
{
    public class UserService : BaseDatabaseService<User>
    {
        private readonly RoleService RoleService;
        private readonly UserManager<User> UserManager;
        private readonly RoleManager<Role> RoleManager;

        public UserService(
            ILogger<UserService> logger,
            Repository<User> repository,
            RoleService roleService,
            UserManager<User> userManager,
            RoleManager<Role> roleManager) : base(logger, repository)
        {
            RoleService = roleService;
            UserManager = userManager;
        }

        public async Task<User> Get(string username)
        {
            return await Repository.FirstOrDefault(u => u.UserName == username);
        }

        public async Task<IEnumerable<Role>> GetRoles(string username)
        {
            var user = await Get(username);

            return await GetRoles(user);
        }

        public async Task<IEnumerable<Role>> GetRoles(User user)
        {
            var roleNames = await UserManager.GetRolesAsync(user);

            return await RoleService.Get(r => roleNames.Contains(r.Name));
        }

        public async Task<bool> IsInRole(User user, string roleName)
        {
            return await UserManager.IsInRoleAsync(user, roleName);
        }

        public override async Task<User> Add(User user)
        {
            var result = await UserManager.CreateAsync(user);

            if (result.Succeeded)
                return await UserManager.FindByNameAsync(user.UserName);
            else
                return null;
        }

        public async Task AddToRole(User user, string roleName)
        {
            await UserManager.AddToRoleAsync(user, roleName);
        }

        public async Task AddToRoles(User user, IEnumerable<string> roleNames)
        {
            var roles = await RoleService.Get(r => roleNames.Contains(r.Name));
        }

        public async Task RemoveFromRole(User user, Role role)
        {
            await RemoveFromRole(user, role.Name);
        }

        public async Task RemoveFromRole(User user, string roleName)
        {
            var role = user.Roles.FirstOrDefault(u => u.Name == roleName);

            if (role != null)
            {
                user.Roles.Remove(role);

                await Update(user);
            }
        }

        public async Task<bool> CheckPassword(User user, string password)
        {
            return await UserManager.CheckPasswordAsync(user, password);
        }

        public async Task<bool> ChangePassword(User user, string currentPassword, string newPassword)
        {
            var result = await UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

            return result.Succeeded;
        }

        public async Task<bool> ChangePassword(User user, string newPassword)
        {
            var token = await UserManager.GeneratePasswordResetTokenAsync(user);

            var result = await UserManager.ResetPasswordAsync(user, token, newPassword);

            return result.Succeeded;
        }

        public async Task SignOut()
        {

        }
    }
}
