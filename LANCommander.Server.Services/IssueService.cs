﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class IssueService : BaseDatabaseService<Issue>
    {
        public IssueService(
            ILogger<IssueService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory)
        {
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
