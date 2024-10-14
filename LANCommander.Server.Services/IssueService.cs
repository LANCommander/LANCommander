using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class IssueService : BaseDatabaseService<Issue>
    {
        public IssueService(
            ILogger<IssueService> logger,
            Repository<Issue> repository) : base(logger, repository)
        {
        }

        public async Task ResolveAsync(Guid issueId)
        {
            var issue = await Get(issueId);

            issue.ResolvedOn = DateTime.Now;
            // issue.ResolvedBy = await GetCurrentUserAsync();

            await Update(issue);
        }
    }
}
