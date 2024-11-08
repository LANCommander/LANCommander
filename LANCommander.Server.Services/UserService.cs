using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService, IBaseDatabaseService<User>
    {
        private readonly IdentityContext IdentityContext;

        public Repository<User> Repository { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public UserService(
            ILogger<UserService> logger,
            Repository<User> repository,
            RoleService roleService,
            IdentityContextFactory identityContextFactory) : base(logger)
        {
            IdentityContext = identityContextFactory.Create();
        }

        public async Task<User> Get(string userName)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.FindByNameAsync(userName);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<IEnumerable<Role>> GetRoles(string userName)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var roleNames = await IdentityContext.UserManager.GetRolesAsync(user);

                return await IdentityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<bool> IsInRole(string userName, string roleName)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                return await IdentityContext.UserManager.IsInRoleAsync(user, roleName);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<User> Add(User user)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var result = await IdentityContext.UserManager.CreateAsync(user);

                if (result.Succeeded)
                    return await IdentityContext.UserManager.FindByNameAsync(user.UserName);
                else
                    return null;
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task AddToRole(string userName, string roleName)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                await IdentityContext.UserManager.AddToRoleAsync(user, roleName);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task AddToRoles(string userName, IEnumerable<string> roleNames)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                await IdentityContext.UserManager.AddToRolesAsync(user, roleNames);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task RemoveFromRole(string userName, string roleName)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                await IdentityContext.UserManager.RemoveFromRoleAsync(user, roleName);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<bool> CheckPassword(string userName, string password)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                return await IdentityContext.UserManager.CheckPasswordAsync(user, password);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string currentPassword, string newPassword)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var result = await IdentityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

                return result;
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string newPassword)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var token = await IdentityContext.UserManager.GeneratePasswordResetTokenAsync(user);

                return await IdentityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task SignOut()
        {

        }

        public async Task<ICollection<User>> Get()
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.Users.ToListAsync();
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<User> Get(Guid id)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.FindByIdAsync(id.ToString());
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<ICollection<User>> Get(Expression<Func<User, bool>> predicate)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.Users.Where(predicate).ToListAsync();
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<User> FirstOrDefault(Expression<Func<User, bool>> predicate)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.Users.FirstOrDefaultAsync(predicate);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<User> FirstOrDefault<TKey>(Expression<Func<User, bool>> predicate, Expression<Func<User, TKey>> orderKeySelector)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                return await IdentityContext.UserManager.Users.Where(predicate).OrderBy(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<bool> Exists(Guid id)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByIdAsync(id.ToString());

                return user != null;
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<ExistingEntityResult<User>> AddMissing(Expression<Func<User, bool>> predicate, User entity)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var result = new ExistingEntityResult<User>();

                var user = await IdentityContext.UserManager.Users.FirstOrDefaultAsync(predicate);

                if (user == null)
                {
                    await IdentityContext.UserManager.CreateAsync(entity);

                    result.Existing = false;
                    result.Value = await IdentityContext.UserManager.FindByNameAsync(user.UserName);
                }
                else
                {
                    result.Existing = true;
                    result.Value = user;
                }

                return result;
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task<User> Update(User entity)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByIdAsync(entity.Id.ToString());

                user.UserName = entity.UserName;
                user.PhoneNumber = entity.PhoneNumber;
                user.Email = entity.Email;
                user.TwoFactorEnabled = entity.TwoFactorEnabled;
                user.Alias = entity.Alias;
                user.Approved = entity.Approved;
                user.ApprovedOn = entity.ApprovedOn;

                await IdentityContext.UserManager.UpdateAsync(user);

                return user;
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }

        public async Task Delete(User entity)
        {
            try
            {
                await IdentityContext.DatabaseContext.ContextMutex.WaitAsync();

                var user = await IdentityContext.UserManager.FindByIdAsync(entity.Id.ToString());

                await IdentityContext.UserManager.DeleteAsync(user);
            }
            finally
            {
                IdentityContext.DatabaseContext.ContextMutex.Release();
            }
        }
    }
}
