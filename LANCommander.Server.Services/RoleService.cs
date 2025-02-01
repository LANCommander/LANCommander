using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Data;
using LANCommander.Server.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class RoleService(
        ILogger<RoleService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory,
        CollectionService collectionService,
        IdentityContextFactory identityContextFactory,
        RoleManager<Role> roleManager) : BaseDatabaseService<Role>(logger, cache, mapper, contextFactory)
    {
        public const string AdministratorRoleName = "Administrator";

        private IdentityContext _identityContext;

        public override void Initialize()
        {
            _identityContext = identityContextFactory.Create();
        }

        public override Task<Role> UpdateAsync(Role entity)
        {
            throw new NotImplementedException();
        }

        public async Task<Role> AddAsync(Role role)
        {
            var result = await roleManager.CreateAsync(role);

            if (result.Succeeded)
                return await roleManager.FindByNameAsync(role.Name);
            
            throw new AddRoleException(result, "Could not create role");
        }

        public async Task<Role> GetAsync(string roleName)
        {
            return await FirstOrDefaultAsync(r => r.Name == roleName);
        }

        public async Task<T> GetAsync<T>(string roleName)
        {
            var role = await FirstOrDefaultAsync(r => r.Name == roleName);

            return mapper.Map<T>(role);
        }

        public async Task<Role> AssignCollections(Guid roleId, IEnumerable<Guid> collectionIds)
        {
            var role = await Include(r => r.Collections).GetAsync(roleId);

            if (role.Collections == null)
                role.Collections = new List<Collection>();

            foreach (var collectionId in collectionIds.Where(id => !role.Collections.Any(c => c.Id == id)))
            {
                var collection = await collectionService.GetAsync(collectionId);

                role.Collections.Add(collection);
            }

            foreach (var collection in role.Collections.Where(c => !collectionIds.Contains(c.Id)))
            {
                role.Collections.Remove(collection);
            }

            role = await UpdateAsync(role);

            return role;
        }

        public async Task<IEnumerable<User>> GetUsersAsync(string roleName)
        {
            return await _identityContext.UserManager.GetUsersInRoleAsync(roleName);
        }
    }
}
