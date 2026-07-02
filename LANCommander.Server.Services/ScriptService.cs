using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;
using AutoMapper;
using LANCommander.SDK;
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
        GameVersionService gameVersionService,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Script>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Script> AddAsync(Script script)
        {
            using var context = await contextFactory.CreateDbContextAsync();

            await cache.ExpireGameCacheAsync(script?.GameId);

            // Scripts for a game are version-scoped. Attach new scripts to the game's current
            // version here so callers don't have to resolve and pass it in themselves.
            if (script.GameId.HasValue && script.GameId != Guid.Empty
                && (script.GameVersionId == null || script.GameVersionId == Guid.Empty))
                script.GameVersionId = await gameVersionService.GetOrCreateLatestIdAsync(script.GameId.Value);

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
        
        public IEnumerable<Snippet> GetSnippets()
        {
            var storagePath = settingsProvider.CurrentValue.Server.Scripts.Snippets.StoragePath;
            
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                // Set default storage path
                storagePath = AppPaths.GetConfigPath("Snippets");
                
                settingsProvider.Update(s =>
                {
                    s.Server.Scripts.Snippets.StoragePath = storagePath;
                });
            }
            
            var files = Directory.GetFiles(storagePath, "*.ps1", SearchOption.AllDirectories);

            return files.Select(f =>
            {
                var split = f.Substring(storagePath.Length).TrimStart('/').Split(Path.DirectorySeparatorChar);

                return new Snippet
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    Group = split[1],
                    Content = File.ReadAllText(f)
                };
            });
        }

        private string GetSnippetsStoragePath()
        {
            var storagePath = settingsProvider.CurrentValue.Server.Scripts.Snippets.StoragePath;

            if (string.IsNullOrWhiteSpace(storagePath))
            {
                storagePath = AppPaths.GetConfigPath("Snippets");

                settingsProvider.Update(s =>
                {
                    s.Server.Scripts.Snippets.StoragePath = storagePath;
                });
            }

            if (!Directory.Exists(storagePath))
                Directory.CreateDirectory(storagePath);

            return storagePath;
        }

        private string GetSnippetPath(string group, string name) =>
            Path.Combine(GetSnippetsStoragePath(), group, $"{name}.ps1");

        public Snippet GetSnippet(string group, string name)
        {
            var path = GetSnippetPath(group, name);

            if (!File.Exists(path))
                return null;

            return new Snippet
            {
                Group = group,
                Name = name,
                Content = File.ReadAllText(path),
            };
        }

        public void SaveSnippet(Snippet snippet)
        {
            var path = GetSnippetPath(snippet.Group, snippet.Name);
            var directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, snippet.Content ?? string.Empty);
        }

        public void DeleteSnippet(string group, string name)
        {
            var path = GetSnippetPath(group, name);

            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
