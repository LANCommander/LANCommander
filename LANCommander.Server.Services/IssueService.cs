using System.Configuration;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
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
        IDbContextFactory<DatabaseContext> contextFactory,
        IPluginEventBus pluginEventBus,
        IssueAttachmentService attachmentService) : BaseDatabaseService<Issue>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Issue> AddAsync(Issue entity)
        {
            var issue = await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(i => i.Game);
                await context.UpdateRelationshipAsync(i => i.ResolvedBy);
            });

            await pluginEventBus.PublishAsync(new IssueCreatedEvent(issue.Id, issue.GameId, issue.CreatedById, issue.Description));

            return issue;
        }

        public override async Task<Issue> UpdateAsync(Issue entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(i => i.Game);
                await context.UpdateRelationshipAsync(i => i.ResolvedBy);
            });
        }

        /// <summary>Deletes the issue's attached files too; the database only removes their rows.</summary>
        public override async Task DeleteAsync(Issue entity)
        {
            foreach (var attachment in await attachmentService.GetAsync(a => a.IssueId == entity.Id))
                await attachmentService.DeleteAsync(attachment);

            await base.DeleteAsync(entity);
        }

        public async Task SetPinnedAsync(Guid issueId, bool pinned)
        {
            var issue = await GetAsync(issueId);

            issue.IsPinned = pinned;

            await UpdateAsync(issue);
        }

        public async Task ResolveAsync(Guid issueId, Guid userId)
        {
            var issue = await GetAsync(issueId);
            var user = await userService.GetAsync(userId);

            issue.ResolvedOn = DateTime.UtcNow;
            issue.ResolvedBy = user;

            await UpdateAsync(issue);

            await pluginEventBus.PublishAsync(new IssueResolvedEvent(issue.Id, issue.GameId, userId));
        }
    }
}
