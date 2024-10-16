using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class UserService : BaseDatabaseService<User>
    {
        private readonly RoleService RoleService;

        public UserService(
            ILogger<UserService> logger,
            Repository<User> repository,
            RoleService roleService) : base(logger, repository)
        {
            RoleService = roleService;
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
            return user.Roles;
        }

        public async Task<bool> IsInRole(User user, string roleName)
        {
            if (user.Roles == null)
                return false;

            return user.Roles.Any(r => r.Name == roleName);
        }

        public async Task AddToRole(User user, string roleName)
        {
            var role = await RoleService.Get(roleName);

            if (user.Roles == null)
                user.Roles = new List<Role>();

            user.Roles.Add(role);

            await Update(user);
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
            // return await UserManager.CheckPasswordAsync(user, password);
            return false;
        }

        public async Task<bool> ChangePassword(User user, string currentPassword, string newPassword)
        {
            /*var result = await UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

            return result.Succeeded;*/
            return false;
        }

        public async Task<bool> ChangePassword(User user, string newPassword)
        {
            /*var token = await UserManager.GeneratePasswordResetTokenAsync(user);

            var result = await UserManager.ResetPasswordAsync(user, token, newPassword);

            return result.Succeeded;*/
            return false;
        }

        public async Task SignOut()
        {

        }
    }
}
