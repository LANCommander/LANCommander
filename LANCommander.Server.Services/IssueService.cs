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
        public override Task<Issue> UpdateAsync(Issue entity)
        {
            throw new NotImplementedException();
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
