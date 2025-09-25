using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class UserService(
        ILogger<UserService> logger,
        DatabaseContext dbContext,
        AuthenticationService authenticationService) : BaseDatabaseService<User>(dbContext, logger)
    {
        public override async Task<User> GetAsync(Guid id)
        {
            return await Context
                .Users
                .AsQueryable()
                .Include(u => u.Avatar)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User> GetCurrentUser()
        {
            return await Context
                .Users
                .AsQueryable()
                .Include(u => u.Avatar)
                .FirstOrDefaultAsync(u => u.Id == authenticationService.GetUserId());
        }
        
        public async Task<string> GetAliasAsync(Guid id)
        {
            var user = await GetAsync(id);

            return String.IsNullOrWhiteSpace(user.Alias) ? user.UserName : user.Alias;
        }
    }
}
