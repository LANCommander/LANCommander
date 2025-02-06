using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class IssueService(
        ILogger<IssueService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Issue>(logger, cache, mapper, contextFactory)
    {
        public override async Task<Issue> UpdateAsync(Issue entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(i => i.Game);
                await context.UpdateRelationshipAsync(i => i.ResolvedBy);
            });
        }
        
        public async Task ResolveAsync(Guid issueId)
        {
            var issue = await GetAsync(issueId);

            issue.ResolvedOn = DateTime.UtcNow;
            // issue.ResolvedBy = await GetCurrentUserAsync();

            await UpdateAsync(issue);
        }
    }
}
