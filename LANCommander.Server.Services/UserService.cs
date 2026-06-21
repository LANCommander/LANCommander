using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Exceptions;
using LANCommander.Server.Data;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class UserService : BaseService, IBaseDatabaseService<User>
    {
        private readonly IdentityContextFactory _identityContextFactory;
        private readonly CollectionService CollectionService;
        private readonly IDbContextFactory<DatabaseContext> ContextFactory;
        private readonly IMapper Mapper;
        private readonly IFusionCache Cache;
        private readonly IOptions<IdentityOptions> _identityOptions;

        protected readonly List<Func<IQueryable<User>, IQueryable<User>>> _modifiers = new();

        public UserService(
            ILogger<UserService> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            IMapper mapper,
            IFusionCache cache,
            CollectionService collectionService,
            IDbContextFactory<DatabaseContext> contextFactory,
            IdentityContextFactory identityContextFactory,
            IOptions<IdentityOptions> identityOptions) : base(logger, settingsProvider)
        {
            _identityContextFactory = identityContextFactory;
            CollectionService = collectionService;
            ContextFactory = contextFactory;
            Mapper = mapper;
            Cache = cache;
            _identityOptions = identityOptions;
        }

        public void Reconfigure()
        {
            var options = _identityOptions.Value;
            if (options == null)
                return;

            options.Password.RequireNonAlphanumeric = _settingsProvider.CurrentValue.Server.Authentication.PasswordRequireNonAlphanumeric;
            options.Password.RequireLowercase = _settingsProvider.CurrentValue.Server.Authentication.PasswordRequireLowercase;
            options.Password.RequireUppercase = _settingsProvider.CurrentValue.Server.Authentication.PasswordRequireUppercase;
            options.Password.RequireDigit = _settingsProvider.CurrentValue.Server.Authentication.PasswordRequireDigit;
            options.Password.RequiredLength = _settingsProvider.CurrentValue.Server.Authentication.PasswordRequiredLength;
        }

        public async Task<User> GetAsync(string userName)
        {
            return await FirstOrDefaultAsync(u => u.UserName.ToUpper() == userName.ToUpper());
        }

        public async Task<T> GetAsync<T>(string userName)
        {
            return await FirstOrDefaultAsync<T>(u => u.UserName.ToUpper() == userName.ToUpper());
        }

        public async Task<IEnumerable<Role>> GetRolesAsync(User user)
        {
            var roles = await Cache.GetOrSetAsync($"User/{user.Id}/Roles", async _ =>
            {
                try
                {
                    user = await Query(q =>
                    {
                        return q
                            .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role);
                    }).FirstOrDefaultAsync(u => u.Id == user.Id);

                    return user.Roles;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not get roles for user {Username}", user.UserName);
                    return new List<Role>();
                }
            }, tags: ["User/Security", "User/Roles", $"User/{user.Id}"]);

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

        public async Task<bool> ExistsAsync(Expression<Func<User, bool>> predicate)
        {
            using var context = await ContextFactory.CreateDbContextAsync();
            return await context.Users.AnyAsync(predicate);
        }

        public Task<User> AddAsync(User user)
        {
            return AddAsync(user, bypassPasswordPolicy: false);
        }

        public async Task<User> AddAsync(User user, bool bypassPasswordPolicy, string? password = null)
        {
            IdentityResult result;
            if (bypassPasswordPolicy && !string.IsNullOrEmpty(password))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.ConcurrencyStamp = Guid.NewGuid().ToString();

                // hash & set password
                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, password);

                // insert & save
                using var context = await ContextFactory.CreateDbContextAsync();
                context.Users!.Add(user);
                await context.SaveChangesAsync();

                result = IdentityResult.Success;
            }
            else
            {
                using var identityContext = await _identityContextFactory.CreateAsync();
                result = await identityContext.UserManager.CreateAsync(user);
            }

            if (result.Succeeded)
            {
                using var findContext = await _identityContextFactory.CreateAsync();
                return await findContext.UserManager.FindByNameAsync(user.UserName);
            }
            else
                throw new UserRegistrationException(result, "Could not create user");
        }

        public async Task AddToRoleAsync(string userName, string roleName)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);
            await identityContext.UserManager.AddToRoleAsync(user, roleName);
        }

        public async Task AddToRolesAsync(string userName, IEnumerable<string> roleNames)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);
            var result = await identityContext.UserManager.AddToRolesAsync(user, roleNames);

            await Cache.RemoveByTagAsync(["User/Security", "User/Roles", $"User/{user.Id}", $"Library/{user.Id}"]);

            if (!result.Succeeded)
                throw new AddRoleException(result, "Could not add roles");
        }

        public async Task RemoveFromRole(string userName, string roleName)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);
            await identityContext.UserManager.RemoveFromRoleAsync(user, roleName);
        }

        public async Task<bool> CheckPassword(string userName, string password)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);
            return await identityContext.UserManager.CheckPasswordAsync(user, password);
        }

        public async Task<IdentityResult> CheckRegister(User user, string password)
        {
            var registerErrors = new List<IdentityError>();

            using var identityContext = await _identityContextFactory.CreateAsync();
            var userManager = identityContext.UserManager;

            foreach (var validator in userManager.UserValidators ?? [])
            {
                var result = await validator.ValidateAsync(userManager, user);
                if (!result.Succeeded)
                {
                    registerErrors.AddRange(result.Errors);
                }
            }

            foreach (var validator in userManager.PasswordValidators ?? [])
            {
                var result = await validator.ValidateAsync(userManager, user, password);
                if (!result.Succeeded)
                {
                    registerErrors.AddRange(result.Errors);
                }
            }

            return registerErrors.Count > 0
                ? IdentityResult.Failed(registerErrors.ToArray())
                : IdentityResult.Success;
        }

        public async Task<IdentityResult> ChangePassword(string userName, string currentPassword, string newPassword)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);
            var result = await identityContext.UserManager.ChangePasswordAsync(user, currentPassword, newPassword);

            return result;
        }

        public Task<IdentityResult> ChangePassword(string userName, string newPassword)
        {
            return ChangePassword(userName, newPassword, bypassPolicy: false);
        }

        public async Task<IdentityResult> ChangePassword(string userName, string newPassword, bool bypassPolicy)
        {
            IdentityResult result;

            using var identityContext = await _identityContextFactory.CreateAsync();
            var user = await identityContext.UserManager.FindByNameAsync(userName);

            if (bypassPolicy && identityContext.UserManager.PasswordValidators.Any())
            {
                await identityContext.UserManager.RemovePasswordAsync(user);

                result = await identityContext.UserManager.AddPasswordAsync(user, newPassword);
            }
            else
            {
                var token = await identityContext.UserManager.GeneratePasswordResetTokenAsync(user);

                result = await identityContext.UserManager.ResetPasswordAsync(user, token, newPassword);
            }

            return result;
        }

        public async Task SignOut()
        {

        }

        public virtual async Task<bool> AnyAsync()
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();

                var queryable = context.Set<User>().AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.AnyAsync();
            }
            catch
            {
                return false;
            }
            finally
            {
                Reset();
            }
        }

        public async Task<ICollection<User>> GetAsync()
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<ICollection<T>> GetAsync<T>()
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable
                    .ProjectTo<T>(Mapper.ConfigurationProvider)
                    .ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<User> GetAsync(Guid id)
        {
            try
            {
                return await FirstOrDefaultAsync(u => u.Id == id);
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> GetAsync<T>(Guid id)
        {
            try
            {
                return await FirstOrDefaultAsync<T>(u => u.Id == id);
            }
            finally
            {
                Reset();
            }
        }

        public async Task<ICollection<User>> GetAsync(Expression<Func<User, bool>> predicate)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.Where(predicate).ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<ICollection<T>> GetAsync<T>(Expression<Func<User, bool>> predicate)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable
                    .Where(predicate)
                    .ProjectTo<T>(Mapper.ConfigurationProvider)
                    .ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<User> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.FirstOrDefaultAsync(predicate);
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstOrDefaultAsync<T>(Expression<Func<User, bool>> predicate)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable
                    .Where(predicate)
                    .ProjectTo<T>(Mapper.ConfigurationProvider)
                    .FirstOrDefaultAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<User> FirstOrDefaultAsync<TKey>(Expression<Func<User, bool>> predicate, Expression<Func<User, TKey>> orderKeySelector)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable
                    .Where(predicate)
                    .OrderBy(orderKeySelector)
                    .FirstOrDefaultAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstOrDefaultAsync<T, TKey>(Expression<Func<User, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var queryable = context.Users.AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable
                    .Where(predicate)
                    .ProjectTo<T>(Mapper.ConfigurationProvider)
                    .OrderBy(orderKeySelector)
                    .FirstOrDefaultAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            using var context = await ContextFactory.CreateDbContextAsync();
            return await context.Users.AnyAsync(u => u.Id == id);
        }

        public async Task<ExistingEntityResult<User>> AddMissingAsync(Expression<Func<User, bool>> predicate, User entity)
        {
            var result = new ExistingEntityResult<User>();

            var user = await FirstOrDefaultAsync(predicate);

            if (user == null)
            {
                using var identityContext = await _identityContextFactory.CreateAsync();
                await identityContext.UserManager.CreateAsync(entity);

                result.Existing = false;
                result.Value = await identityContext.UserManager.FindByNameAsync(entity.UserName);
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
            using var identityContext = await _identityContextFactory.CreateAsync();

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

        public async Task DeleteAsync(User entity)
        {
            using var identityContext = await _identityContextFactory.CreateAsync();

            var user = await identityContext.UserManager.FindByIdAsync(entity.Id.ToString());

            await identityContext.UserManager.DeleteAsync(user);
        }

        public IBaseDatabaseService<User> AsNoTracking()
        {
            return Query((queryable) =>
            {
                return queryable.AsNoTracking();
            });
        }

        public IBaseDatabaseService<User> AsSplitQuery()
        {
            return Query((queryable) =>
            {
                return queryable.AsSplitQuery();
            });
        }

        public IBaseDatabaseService<User> Query(Func<IQueryable<User>, IQueryable<User>> modifier)
        {
            _modifiers.Add(modifier);

            return this;
        }

        public IBaseDatabaseService<User> Include(params Expression<Func<User, object>>[] expressions)
        {
            return Query((queryable) =>
            {
                foreach (var expression in expressions)
                {
                    queryable = queryable.Include(expression);
                }

                return queryable;
            });
        }

        public IBaseDatabaseService<User> SortBy(Expression<Func<User, object>> expression, SortDirection direction = SortDirection.Ascending)
        {
            switch (direction)
            {
                case SortDirection.Descending:
                    return Query((queryable) =>
                    {
                        return queryable.OrderByDescending(expression);
                    });
                case SortDirection.Ascending:
                default:
                    return Query((queryable) =>
                    {
                        return queryable.OrderBy(expression);
                    });
            }
        }

        protected void Reset()
        {
            _modifiers.Clear();
        }

        public void Dispose()
        {
        }
    }
}
