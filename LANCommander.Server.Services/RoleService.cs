using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseDatabaseService<Role>
    {
        public const string AdministratorRoleName = "Administrator";

        public RoleService(
            ILogger<RoleService> logger,
            Repository<Role> repository) : base(logger, repository) { }

        public async Task<Role> Get(string roleName)
        {
            return await Repository.FirstOrDefault(r => r.Name == roleName);
        }

        public async Task<IEnumerable<User>> GetUsers(string roleName)
        {
            var role = await Get(roleName);

            return role.Users;
        }
    }
}
