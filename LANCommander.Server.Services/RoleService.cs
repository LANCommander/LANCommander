using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseService
    {
        private readonly RoleManager<Role> RoleManager;

        public RoleService(
            ILogger<RoleService> logger,
            RoleManager<Role> roleManager) : base(logger)
        {
            RoleManager = roleManager;
        }

        public async Task<IEnumerable<Role>> Get()
        {
            return await RoleManager.Roles.ToListAsync();
        }

        public async Task<Role> Get(Guid id)
        {
            return await RoleManager.FindByIdAsync(id.ToString());
        }

        public async Task<Role> Get(string roleName)
        {
            return await RoleManager.FindByNameAsync(roleName);
        }

        public async Task<Role> Add(Role role)
        {
            await RoleManager.CreateAsync(role);

            return await Get(role.Name);
        }

        public async Task<Role> Update(Role role)
        {
            await RoleManager.UpdateAsync(role);

            return await RoleManager.FindByIdAsync(role.Id.ToString());
        }

        public async Task Delete(Role role)
        {
            await RoleManager.DeleteAsync(role);
        }
    }
}
