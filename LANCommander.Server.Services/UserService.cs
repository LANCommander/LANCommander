using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService
    {
        private readonly DatabaseContext DatabaseContext;
        private readonly UserManager<User> UserManager;
        private readonly RoleManager<Role> RoleManager;

        public UserService(
            ILogger<UserService> logger,
            DatabaseContext databaseContext,
            UserManager<User> userManager,
            RoleManager<Role> roleManager) : base(logger)
        {
            DatabaseContext = databaseContext;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        public async Task<IEnumerable<User>> Get()
        {
            return UserManager.Users;
        }

        public async Task<User> Get(Guid id)
        {
            return await UserManager.FindByIdAsync(id.ToString());
        }

        public async Task<User> Get(string username)
        {
            return await UserManager.FindByNameAsync(username);
        }

        public async Task<IEnumerable<User>> GetInRole(string roleName)
        {
            return await UserManager.GetUsersInRoleAsync(roleName);
        }

        public async Task<User> Add(User user)
        {
            var result = await UserManager.CreateAsync(user);

            if (result.Succeeded)
                return await Get(user.UserName);
            else
                return null;
        }

        public async Task<User> Update(User user)
        {
            await UserManager.UpdateAsync(user);

            return await Get(user.Id);
        }

        public async Task Delete(User user)
        {
            await UserManager.DeleteAsync(user);
        }

        public async Task<IEnumerable<Role>> GetRoles(string username)
        {
            var user = await Get(username);

            return await GetRoles(user);
        }

        public async Task<IEnumerable<Role>> GetRoles(User user)
        {
            var roleNames = await UserManager.GetRolesAsync(user);

            var roles = new List<Role>();

            foreach (var roleName in roleNames)
            {
                var role = await RoleManager.FindByNameAsync(roleName);

                if (role != null)
                    roles.Add(role);
            }

            return roles;
        }

        public async Task<IEnumerable<User>> GetUsersInRole(string roleName)
        {
            return await UserManager.GetUsersInRoleAsync(roleName);
        }

        public async Task<bool> IsInRole(User user, string roleName)
        {
            return await UserManager.IsInRoleAsync(user, roleName);
        }

        public async Task AddToRole(User user, string role)
        {
            await UserManager.AddToRoleAsync(user, role);
        }

        public async Task AddToRoles(User user, IEnumerable<string> roles)
        {
            await UserManager.AddToRolesAsync(user, roles);
        }

        public async Task RemoveFromRole(User user, Role role)
        {
            await RemoveFromRole(user, role.Name);
        }

        public async Task RemoveFromRole(User user, string role)
        {
            await UserManager.RemoveFromRoleAsync(user, role);
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

        public async Task<UserCustomField> GetCustomField(Guid userId, string name)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                return repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);
            }
        }

        public async Task UpdateCustomField(Guid userId, string name, string value)
        {
            if (name.Length > 64)
                throw new ArgumentException("Field name must be 64 characters or shorter");

            if (value.Length > 1024)
                throw new ArgumentException("Field value must be 1024 characters or less");

            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

                if (existing.Value == value)
                    return;

                if (existing == null)
                {
                    await repo.Add(new UserCustomField
                    {
                        Name = name,
                        Value = value
                    });

                    await repo.SaveChanges();
                }
                else if (!String.IsNullOrWhiteSpace(value))
                {
                    existing.Value = value;

                    repo.Update(existing);

                    await repo.SaveChanges();
                }
                else
                {
                    await DeleteCustomField(userId, name);
                }
            }
        }

        public async Task DeleteCustomField(Guid userId, string name)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Name == name);

                repo.Delete(existing);
                await repo.SaveChanges();
            }
        }

        public async Task DeleteCustomField(Guid userId, Guid id)
        {
            using (var repo = new Repository<UserCustomField>(DatabaseContext))
            {
                var existing = repo.FirstOrDefault(cf => cf.UserId == userId && cf.Id == id);

                repo.Delete(existing);
                await repo.SaveChanges();
            }
        }
    }
}
