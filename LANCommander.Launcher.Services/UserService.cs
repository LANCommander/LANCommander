using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class UserService : BaseDatabaseService<User>
    {
        private readonly AuthenticationService AuthenticationService;
        public UserService(
            DatabaseContext dbContext,
            SDK.Client client,
            ILogger<UserService> logger,
            AuthenticationService authenticationService) : base(dbContext, client, logger)
        {
            AuthenticationService = authenticationService;
        }

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
                .FirstOrDefaultAsync(u => u.Id == AuthenticationService.GetUserId());
        }
        
        public async Task<string> GetAliasAsync(Guid id)
        {
            var user = await GetAsync(id);

            return String.IsNullOrWhiteSpace(user.Alias) ? user.UserName : user.Alias;
        }
    }
}
