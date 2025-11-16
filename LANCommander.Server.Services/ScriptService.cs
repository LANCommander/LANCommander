using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;
using AutoMapper;
using LANCommander.SDK.Extensions;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ScriptService(
        ILogger<ScriptService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Script>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Script> AddAsync(Script script)
        {
            using var context = await contextFactory.CreateDbContextAsync();
            
            await cache.ExpireGameCacheAsync(script?.GameId);

            if (script.RedistributableId?.IsNullOrEmpty() ?? false)
            {
                var games = await context
                    .Games
                    .Include(g => g.Redistributables)
                    .Where(g => g.Redistributables.Any(r => r.Id == script.RedistributableId))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var game in games)
                    await cache.ExpireGameCacheAsync(game.Id);
            }
            
            return await base.AddAsync(script, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.Redistributable);
                await context.UpdateRelationshipAsync(s => s.Server);
            });
        }
        
        public override async Task<Script> UpdateAsync(Script script)
        {
            using var context = await contextFactory.CreateDbContextAsync();
            
            await cache.ExpireGameCacheAsync(script?.GameId);

            if (script.RedistributableId?.IsNullOrEmpty() ?? false)
            {
                var games = await context
                    .Games
                    .Include(g => g.Redistributables)
                    .Where(g => g.Redistributables.Any(r => r.Id == script.RedistributableId))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var game in games)
                    await cache.ExpireGameCacheAsync(game.Id);
            }
            
            return await base.UpdateAsync(script, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.Redistributable);
                await context.UpdateRelationshipAsync(s => s.Server);
            });
        }

        public override async Task DeleteAsync(Script script)
        {
            using var context = await contextFactory.CreateDbContextAsync();
            
            await cache.ExpireGameCacheAsync(script?.GameId);

            if (script.RedistributableId?.IsNullOrEmpty() ?? false)
            {
                var games = await context
                    .Games
                    .Include(g => g.Redistributables)
                    .Where(g => g.Redistributables.Any(r => r.Id == script.RedistributableId))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var game in games)
                    await cache.ExpireGameCacheAsync(game.Id);
            }
            
            await base.DeleteAsync(script);
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
