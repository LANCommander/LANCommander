using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ScriptService(
        ILogger<ScriptService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Script>(logger, cache, mapper, contextFactory)
    {
        public async override Task<Script> UpdateAsync(Script entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.Redistributable);
                await context.UpdateRelationshipAsync(s => s.Server);
            });
        }
        
        public static IEnumerable<Snippet> GetSnippets()
        {
            var files = Directory.GetFiles(@"Snippets", "*.ps1", SearchOption.AllDirectories);

            return files.Select(f =>
            {
                var split = f.Split(Path.DirectorySeparatorChar);

                return new Snippet()
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    Group = split[1],
                    Content = File.ReadAllText(f)
                };
            });
        }
    }
}
