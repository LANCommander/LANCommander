using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using System.Linq.Expressions;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService, IBaseDatabaseService<User>
    {
        private readonly IdentityContextFactory IdentityContextFactory;

        public Repository<User> Repository { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public UserService(
            ILogger<UserService> logger,
            Repository<User> repository,
            RoleService roleService,
            IdentityContextFactory identityContextFactory) : base(logger)
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

                return await identityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();
            }
        }

        public async Task<bool> IsInRole(string userName, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                return await identityContext.UserManager.IsInRoleAsync(user, roleName);
            }
        }

        public async Task<User> Add(User user)
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

        public async Task AddToRole(string userName, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                await identityContext.UserManager.AddToRoleAsync(user, roleName);
            }
        }

        public async Task AddToRoles(string userName, IEnumerable<string> roleNames)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                await identityContext.UserManager.AddToRolesAsync(user, roleNames);
            }
        }

        public async Task RemoveFromRole(string userName, string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                await identityContext.UserManager.RemoveFromRoleAsync(user, roleName);
            }
        }

        public async Task<bool> CheckPassword(string userName, string password)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                return await identityContext.UserManager.CheckPasswordAsync(user, password);
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string currentPassword, string newPassword)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                var result = await identityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

                return result;
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string newPassword)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByNameAsync(userName);

                var token = await identityContext.UserManager.GeneratePasswordResetTokenAsync(user);

                return await identityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
            }
        }

        public async Task SignOut()
        {

        }

        public async Task<ICollection<User>> Get()
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.Users.ToListAsync();
            }
        }

        public async Task<User> Get(Guid id)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.FindByIdAsync(id.ToString());
            }
        }

        public async Task<ICollection<User>> Get(Expression<Func<User, bool>> predicate)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.Users.Where(predicate).ToListAsync();
            }
        }

        public async Task<User> FirstOrDefault(Expression<Func<User, bool>> predicate)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.Users.FirstOrDefaultAsync(predicate);
            }
        }

        public async Task<User> FirstOrDefault<TKey>(Expression<Func<User, bool>> predicate, Expression<Func<User, TKey>> orderKeySelector)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.Users.Where(predicate).OrderBy(orderKeySelector).FirstOrDefaultAsync();
            }
        }

        public async Task<bool> Exists(Guid id)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByIdAsync(id.ToString());

                return user != null;
            }
        }

        public async Task<ExistingEntityResult<User>> AddMissing(Expression<Func<User, bool>> predicate, User entity)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var result = new ExistingEntityResult<User>();

                var user = await identityContext.UserManager.Users.FirstOrDefaultAsync(predicate);

                if (user == null)
                {
                    await identityContext.UserManager.CreateAsync(entity);

                    result.Existing = false;
                    result.Value = await identityContext.UserManager.FindByNameAsync(user.UserName);
                }
                else
                {
                    result.Existing = true;
                    result.Value = user;
                }

                return result;
            }
        }

        public async Task<User> Update(User entity)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByIdAsync(entity.Id.ToString());

                user.UserName = entity.UserName;
                user.PhoneNumber = entity.PhoneNumber;
                user.Email = entity.Email;
                user.TwoFactorEnabled = entity.TwoFactorEnabled;
                user.Alias = entity.Alias;
                user.Approved = entity.Approved;
                user.ApprovedOn = entity.ApprovedOn;

                await identityContext.UserManager.UpdateAsync(user);

                return user;
            }
        }

        public async Task Delete(User entity)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var user = await identityContext.UserManager.FindByIdAsync(entity.Id.ToString());

                await identityContext.UserManager.DeleteAsync(user);
            }
        }
    }
}
