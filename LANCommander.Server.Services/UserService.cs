using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using LANCommander.Server.Services.Exceptions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService, IBaseDatabaseService<User>
    {
        private readonly IdentityContext IdentityContext;
        private readonly CollectionService CollectionService;
        private readonly IMapper Mapper;
        private readonly IFusionCache Cache;

        public UserService(
            ILogger<UserService> logger,
            IMapper mapper,
            IFusionCache cache,
            CollectionService collectionService,
            IdentityContextFactory identityContextFactory) : base(logger)
        {
            IdentityContext = identityContextFactory.Create();
            CollectionService = collectionService;
            Mapper = mapper;
        }

        public async Task<User> GetAsync(string userName)
        {
            return await IdentityContext.UserManager.FindByNameAsync(userName);
        }

        public async Task<T> GetAsync<T>(string userName)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            return Mapper.Map<T>(user);
        }

        public async Task<IEnumerable<Role>> GetRolesAsync(User user)
        {
            var roles = await Cache.GetOrSetAsync($"User/{user.Id}/Roles", async _ =>
            {
                var roleNames = await IdentityContext.UserManager.GetRolesAsync(user);
                
                return await IdentityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();
            });

            return roles;
        }

        public async Task<bool> IsInRoleAsync(User user, string roleName)
        {
            var roles = await GetRolesAsync(user);
            
            return roles.Any(r => r.Name == roleName);
        }

        public async Task<IEnumerable<Collection>> GetCollectionsAsync(User user)
        {
            try
            {
                var roles = await GetRolesAsync(user);
                var roleIds = roles.Select(r => r.Id).ToList();

                if (roles.Any(r => r.Name.Equals(RoleService.AdministratorRoleName, StringComparison.OrdinalIgnoreCase)))
                    return await CollectionService.GetAsync();
                else
                    return await CollectionService
                        .Include(c => c.Roles)
                        .GetAsync(c => c.Roles.Any(r => roleIds.Contains(r.Id)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not get collections for user {UserName}", user.UserName);
                return new List<Collection>();
            }
        }

        public async Task<User> AddAsync(User user)
        {
            var result = await IdentityContext.UserManager.CreateAsync(user);
            
            if (result.Succeeded)
                return await IdentityContext.UserManager.FindByNameAsync(user.UserName);
            else
                throw new UserRegistrationException(result, "Could not create user");
        }

        public async Task AddToRoleAsync(string userName, string roleName)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            await IdentityContext.UserManager.AddToRoleAsync(user, roleName);
        }

        public async Task AddToRolesAsync(string userName, IEnumerable<string> roleNames)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            var result = await IdentityContext.UserManager.AddToRolesAsync(user, roleNames);

            if (!result.Succeeded)
                throw new AddRoleException(result, "Could not add roles");
        }

        public async Task RemoveFromRole(string userName, string roleName)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            await IdentityContext.UserManager.RemoveFromRoleAsync(user, roleName);
        }

        public async Task<bool> CheckPassword(string userName, string password)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            return await IdentityContext.UserManager.CheckPasswordAsync(user, password);
        }

        public async Task<IdentityResult> ChangePassword(string userName, string currentPassword, string newPassword)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            var result = await IdentityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

            return result;
        }

        public async Task<IdentityResult> ChangePassword(string userName, string newPassword)
        {
            var user = await IdentityContext.UserManager.FindByNameAsync(userName);

            var token = await IdentityContext.UserManager.GeneratePasswordResetTokenAsync(user);

            return await IdentityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
        }

        public async Task SignOut()
        {

        }

        public async Task<ICollection<User>> GetAsync()
        {
            return await IdentityContext.UserManager.Users.ToListAsync();
        }

        public async Task<ICollection<T>> GetAsync<T>()
        {
            return await IdentityContext
                .UserManager
                .Users
                .ProjectTo<T>(Mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<User> GetAsync(Guid id)
        {
            return await IdentityContext
                .UserManager
                .FindByIdAsync(id.ToString());
        }

        public async Task<T> GetAsync<T>(Guid id)
        {
            var user = await IdentityContext
                .UserManager
                .FindByIdAsync(id.ToString());

            return Mapper.Map<T>(user);
        }

        public async Task<ICollection<User>> GetAsync(Expression<Func<User, bool>> predicate)
        {
            return await IdentityContext
                .UserManager
                .Users
                .Where(predicate)
                .ToListAsync();
        }

        public async Task<ICollection<T>> GetAsync<T>(Expression<Func<User, bool>> predicate)
        {
            return await IdentityContext
                .UserManager
                .Users
                .Where(predicate)
                .ProjectTo<T>(Mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<User> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate)
        {
            return await IdentityContext
                .UserManager
                .Users
                .FirstOrDefaultAsync(predicate);
        }

        public async Task<T> FirstOrDefaultAsync<T>(Expression<Func<User, bool>> predicate)
        {
            return await IdentityContext
                .UserManager
                .Users
                .Where(predicate)
                .ProjectTo<T>(Mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
        }

        public async Task<User> FirstOrDefaultAsync<TKey>(Expression<Func<User, bool>> predicate, Expression<Func<User, TKey>> orderKeySelector)
        {
            return await IdentityContext
                .UserManager
                .Users
                .Where(predicate)
                .OrderBy(orderKeySelector)
                .FirstOrDefaultAsync();
        }

        public async Task<T> FirstOrDefaultAsync<T, TKey>(Expression<Func<User, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await IdentityContext
                .UserManager
                .Users
                .Where(predicate)
                .ProjectTo<T>(Mapper.ConfigurationProvider)
                .OrderBy(orderKeySelector)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            var user = await IdentityContext
                .UserManager
                .FindByIdAsync(id.ToString());

            return user != null;
        }

        public async Task<ExistingEntityResult<User>> AddMissingAsync(Expression<Func<User, bool>> predicate, User entity)
        {
            var result = new ExistingEntityResult<User>();

            var user = await IdentityContext
                .UserManager
                .Users
                .FirstOrDefaultAsync(predicate);

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

        public async Task<User> UpdateAsync(User entity)
        {
            var user = await IdentityContext
                .UserManager
                .FindByIdAsync(entity.Id.ToString());

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

        public async Task DeleteAsync(User entity)
        {
            var user = await IdentityContext
                .UserManager
                .FindByIdAsync(entity.Id.ToString());

            await IdentityContext.UserManager.DeleteAsync(user);
        }

        public IBaseDatabaseService<User> Include(Expression<Func<User, object>> includeExpression)
        {
            throw new NotImplementedException();
        }

        public IBaseDatabaseService<User> Query(Func<IQueryable<User>, IQueryable<User>> modifier)
        {
            throw new NotImplementedException();
        }

        public IBaseDatabaseService<User> Include(params Expression<Func<User, object>>[] expressions)
        {
            throw new NotImplementedException();
        }

        public IBaseDatabaseService<User> SortBy(Expression<Func<User, object>> expression, SortDirection direction)
        {
            throw new NotImplementedException();
        }

        public IBaseDatabaseService<User> AsNoTracking()
        {
            throw new NotImplementedException();
        }

        public Task<PaginatedResults<User>> PaginateAsync(Expression<Func<User, bool>> expression, int pageNumber, int pageSize)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
