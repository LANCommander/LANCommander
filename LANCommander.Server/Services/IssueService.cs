using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services
{
    public class IssueService : BaseDatabaseService<Issue>
    {
        private readonly HttpContext? HttpContext;
        private readonly UserManager<User> UserManager;

        public IssueService(
            ILogger<IssueService> logger,
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            UserManager<User> userManager) : base(logger, dbContext)
        {
            HttpContext = httpContextAccessor.HttpContext;
            UserManager = userManager;
        }

        public async Task ResolveAsync(Guid issueId)
        {
            var issue = await Get(issueId);

            issue.ResolvedOn = DateTime.Now;
            issue.ResolvedBy = await GetCurrentUserAsync();

            await Update(issue);
        }

        private async Task<User> GetCurrentUserAsync()
        {
            if (HttpContext != null && HttpContext.User != null && HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
            {
                var user = await UserManager.FindByNameAsync(HttpContext.User.Identity.Name);

                if (user == null)
                    return null;
                else
                    return user;
            }
            else
                return null;
        }
    }
}
