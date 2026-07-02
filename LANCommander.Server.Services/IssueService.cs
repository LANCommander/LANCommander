using System.Configuration;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class IssueService(
        UserService userService,
        ILogger<IssueService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Issue>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Issue> AddAsync(Issue entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(i => i.Game);
                await context.UpdateRelationshipAsync(i => i.ResolvedBy);
            });
        }

        public override async Task<Issue> UpdateAsync(Issue entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(i => i.Game);
                await context.UpdateRelationshipAsync(i => i.ResolvedBy);
            });
        }
        
        public async Task ResolveAsync(Guid issueId, Guid userId)
        {
            var issue = await GetAsync(issueId);
            var user = await userService.GetAsync(userId);

            issue.ResolvedOn = DateTime.UtcNow;
            issue.ResolvedBy = user;

            await UpdateAsync(issue);
        }
    }
}
