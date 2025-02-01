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

namespace LANCommander.Server.Services
{
    public class UserService : BaseService, IBaseDatabaseService<User>
    {
        private readonly IdentityContext IdentityContext;
        private readonly CollectionService CollectionService;
        private readonly IMapper Mapper;

        public RepositoryFactory repositoryFactory { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public UserService(
            ILogger<UserService> logger,
            IMapper mapper,
            RepositoryFactory repositoryFactory,
            RoleService roleService,
            CollectionService collectionService,
            IdentityContextFactory identityContextFactory) : base(logger)
        {
            IdentityContext = identityContextFactory.Create();
            CollectionService = collectionService;
            Mapper = mapper;
        }

        public async Task<User> GetAsync(string userName)
        {
            try
            {
                return await IdentityContext.UserManager.FindByNameAsync(userName);
            }
            finally
            {
            }
        }

        public async Task<T> GetAsync<T>(string userName)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                return Mapper.Map<T>(user);
            }
            finally
            {
            }
        }

        public async Task<IEnumerable<Role>> GetRolesAsync(string userName)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var roleNames = await IdentityContext.UserManager.GetRolesAsync(user);

                return await IdentityContext.RoleManager.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync();
            }
            finally
            {
            }
        }

        public async Task<bool> IsInRoleAsync(string userName, string roleName)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                return await IdentityContext.UserManager.IsInRoleAsync(user, roleName);
            }
            finally
            {
            }
        }

        public async Task<IEnumerable<Collection>> GetCollectionsAsync(Guid userId)
        {
            try
            {
                var user = await GetAsync(userId);
                var roles = await GetRolesAsync(user.UserName);
                var roleIds = roles.Select(r => r.Id).ToList();

                if (roles.Any(r => r.Name == RoleService.AdministratorRoleName))
                    return await CollectionService.GetAsync();
                else
                    return await CollectionService
                    .Include(c => c.Roles)
                    .GetAsync(c => c.Roles.Any(r => roleIds.Contains(r.Id)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not get user collections");
                return new List<Collection>();
            }
        }

        public async Task<User> AddAsync(User user)
        {
            try
            {
                var result = await IdentityContext.UserManager.CreateAsync(user);
                
                if (result.Succeeded)
                    return await IdentityContext.UserManager.FindByNameAsync(user.UserName);
                else
                    throw new UserRegistrationException(result, "Could not create user");
            }
            finally
            {
            }
        }

        public async Task AddToRoleAsync(string userName, string roleName)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                await IdentityContext.UserManager.AddToRoleAsync(user, roleName);
            }
            finally
            {
            }
        }

        public async Task AddToRolesAsync(string userName, IEnumerable<string> roleNames)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var result = await IdentityContext.UserManager.AddToRolesAsync(user, roleNames);

                if (!result.Succeeded)
                    throw new AddRoleException(result, "Could not add roles");
            }
            finally
            {
            }
        }

        public async Task RemoveFromRole(string userName, string roleName)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                await IdentityContext.UserManager.RemoveFromRoleAsync(user, roleName);
            }
            finally
            {
            }
        }

        public async Task<bool> CheckPassword(string userName, string password)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                return await IdentityContext.UserManager.CheckPasswordAsync(user, password);
            }
            finally
            {
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string currentPassword, string newPassword)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var result = await IdentityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

                return result;
            }
            finally
            {
            }
        }

        public async Task<IdentityResult> ChangePassword(string userName, string newPassword)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByNameAsync(userName);

                var token = await IdentityContext.UserManager.GeneratePasswordResetTokenAsync(user);

                return await IdentityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
            }
        }

        public async Task SignOut()
        {

        }

        public async Task<ICollection<User>> GetAsync()
        {
            try
            {
                return await IdentityContext.UserManager.Users.ToListAsync();
            }
            finally
            {
            }
        }

        public async Task<ICollection<T>> GetAsync<T>()
        {
            try
            {
                return await IdentityContext.UserManager.Users.ProjectTo<T>(Mapper.ConfigurationProvider).ToListAsync();
            }
            finally
            {
            }
        }

        public async Task<User> GetAsync(Guid id)
        {
            try
            {
                return await IdentityContext.UserManager.FindByIdAsync(id.ToString());
            }
            finally
            {
            }
        }

        public async Task<T> GetAsync<T>(Guid id)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByIdAsync(id.ToString());

                return Mapper.Map<T>(user);
            }
            finally
            {
            }
        }

        public async Task<ICollection<User>> GetAsync(Expression<Func<User, bool>> predicate)
        {
            try
            {
                return await IdentityContext.UserManager.Users.Where(predicate).ToListAsync();
            }
            finally
            {
            }
        }

        public async Task<ICollection<T>> GetAsync<T>(Expression<Func<User, bool>> predicate)
        {
            try
            {
                return await IdentityContext.UserManager.Users.Where(predicate).ProjectTo<T>(Mapper.ConfigurationProvider).ToListAsync();
            }
            finally
            {
            }
        }

        public async Task<User> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate)
        {
            try
            {
                return await IdentityContext.UserManager.Users.FirstOrDefaultAsync(predicate);
            }
            finally
            {
            }
        }

        public async Task<T> FirstOrDefaultAsync<T>(Expression<Func<User, bool>> predicate)
        {
            try
            {
                return await IdentityContext.UserManager.Users.Where(predicate).ProjectTo<T>(Mapper.ConfigurationProvider).FirstOrDefaultAsync();
            }
            finally
            {
            }
        }

        public async Task<User> FirstOrDefaultAsync<TKey>(Expression<Func<User, bool>> predicate, Expression<Func<User, TKey>> orderKeySelector)
        {
            try
            {
                return await IdentityContext.UserManager.Users.Where(predicate).OrderBy(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
            }
        }

        public async Task<T> FirstOrDefaultAsync<T, TKey>(Expression<Func<User, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                return await IdentityContext.UserManager.Users.Where(predicate).ProjectTo<T>(Mapper.ConfigurationProvider).OrderBy(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByIdAsync(id.ToString());

                return user != null;
            }
            finally
            {
            }
        }

        public async Task<ExistingEntityResult<User>> AddMissingAsync(Expression<Func<User, bool>> predicate, User entity)
        {
            try
            {
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
            }
        }

        public async Task<User> UpdateAsync(User entity)
        {
            try
            {
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
            }
        }

        public async Task DeleteAsync(User entity)
        {
            try
            {
                var user = await IdentityContext.UserManager.FindByIdAsync(entity.Id.ToString());

                await IdentityContext.UserManager.DeleteAsync(user);
            }
            finally
            {
            }
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
