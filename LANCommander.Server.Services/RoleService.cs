using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseDatabaseService<Role>
    {
        public const string AdministratorRoleName = "Administrator";

        private readonly RoleManager<Role> RoleManager;
        private readonly UserManager<User> UserManager;

        public RoleService(
            ILogger<RoleService> logger,
            Repository<Role> repository,
            RoleManager<Role> roleManager,
            UserManager<User> userManager) : base(logger, repository)
        {
            RoleManager = roleManager;
            UserManager = userManager;
        }

        public override async Task<Role> Add(Role role)
        {
            var result = await RoleManager.CreateAsync(role);

            return await RoleManager.FindByNameAsync(role.Name);
        }

        public async Task<Role> Get(string roleName)
        {
            return await Repository.FirstOrDefault(r => r.Name == roleName);
        }

        public async Task<IEnumerable<User>> GetUsers(string roleName)
        {
            return await UserManager.GetUsersInRoleAsync(roleName);
        }
    }
}
