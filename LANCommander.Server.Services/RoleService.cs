using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Data;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseDatabaseService<Role>
    {
        public const string AdministratorRoleName = "Administrator";

        private readonly IdentityContext IdentityContext;
        private readonly IMapper Mapper;

        private readonly CollectionService CollectionService;

        public RoleService(
            ILogger<RoleService> logger,
            IFusionCache cache,
            IdentityContextFactory identityContextFactory,
            CollectionService collectionService,
            IMapper mapper,
            DatabaseContext databaseContext) : base(logger, cache, databaseContext, mapper)
        {
            IdentityContext = identityContextFactory.Create();
            Mapper = mapper;
            CollectionService = collectionService;
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
