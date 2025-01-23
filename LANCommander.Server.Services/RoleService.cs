using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Data;
using LANCommander.Server.Services.Exceptions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseDatabaseService<Role>
    {
        public const string AdministratorRoleName = "Administrator";

        private readonly IdentityContext IdentityContext;
        private readonly IMapper Mapper;

        private readonly CollectionService CollectionService;
        private readonly RoleManager<Role> RoleManager;

        public RoleService(
            ILogger<RoleService> logger,
            IMapper mapper,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            IdentityContextFactory identityContextFactory,
            CollectionService collectionService,
            RoleManager<Role> roleManager) : base(logger, cache, repositoryFactory)
        {
            IdentityContext = identityContextFactory.Create();
            Mapper = mapper;
            CollectionService = collectionService;
            RoleManager = roleManager;
        }

        public async Task<Role> AddAsync(Role role)
        {
            var result = await RoleManager.CreateAsync(role);

            if (result.Succeeded)
                return await RoleManager.FindByNameAsync(role.Name);
            
            throw new AddRoleException(result, "Could not create role");
        }

        public async Task<Role> GetAsync(string roleName)
        {
            return await FirstOrDefaultAsync(r => r.Name == roleName);
        }

        public async Task<T> GetAsync<T>(string roleName)
        {
            var role = await FirstOrDefaultAsync(r => r.Name == roleName);

            return Mapper.Map<T>(role);
        }

        public async Task<Role> AssignCollections(Guid roleId, IEnumerable<Guid> collectionIds)
        {
            var role = await Include(r => r.Collections).GetAsync(roleId);

            if (role.Collections == null)
                role.Collections = new List<Collection>();

            foreach (var collectionId in collectionIds.Where(id => !role.Collections.Any(c => c.Id == id)))
            {
                var collection = await CollectionService.GetAsync(collectionId);

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
            return await IdentityContext.UserManager.GetUsersInRoleAsync(roleName);
        }
    }
}
