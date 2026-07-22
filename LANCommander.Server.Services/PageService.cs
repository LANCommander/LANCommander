using JetBrains.Annotations;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using YamlDotNet.Core.Tokens;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PageService(
        ILogger<PageService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Page>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Page> AddAsync(Page entity)
        {
            await EnsureNoCycleAsync(entity);

            entity.Slug = entity.Slug.ToRouteSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToRouteSlug();

            entity.Slug = await GenerateUniqueSlugAsync(entity);
            entity.Route = RenderRoute(entity);

            await cache.ExpireAsync(MenuCacheKey);

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.Children);
                await context.UpdateRelationshipAsync(p => p.Games);
                await context.UpdateRelationshipAsync(p => p.Parent);
                await context.UpdateRelationshipAsync(p => p.Redistributables);
                await context.UpdateRelationshipAsync(p => p.Servers);
            });
        }

        public override async Task<Page> UpdateAsync(Page entity)
        {
            await EnsureNoCycleAsync(entity);

            entity.Slug = entity.Slug.ToRouteSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToRouteSlug();

            entity.Slug = await GenerateUniqueSlugAsync(entity);
            entity.Route = RenderRoute(entity);

            await cache.ExpireAsync(GetCacheKey(entity.Route));
            await cache.ExpireAsync(MenuCacheKey);

            var updated = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.Children);
                await context.UpdateRelationshipAsync(p => p.Games);
                await context.UpdateRelationshipAsync(p => p.Parent);
                await context.UpdateRelationshipAsync(p => p.Redistributables);
                await context.UpdateRelationshipAsync(p => p.Servers);
            });

            await RefreshDescendantRoutesAsync(entity);

            return updated;
        }

        public override async Task DeleteAsync(Page entity)
        {
            await cache.ExpireAsync(GetCacheKey(entity.Route));
            await cache.ExpireAsync(MenuCacheKey);

            await base.DeleteAsync(entity);
        }

        public async Task<IReadOnlyList<PageMenuNode>> GetMenuAsync()
        {
            return await cache.GetOrSetAsync<IReadOnlyList<PageMenuNode>>(MenuCacheKey, async _ =>
            {
                var allPages = await GetAsync();

                // Treat a null or empty parent id as "root" so both variants group together.
                var childrenByParent = allPages
                    .OrderBy(p => p.SortOrder)
                    .ToLookup(p => p.ParentId == Guid.Empty ? null : p.ParentId);

                IReadOnlyList<PageMenuNode> Build(Guid? parentId) =>
                    childrenByParent[parentId]
                        .Select(p => new PageMenuNode
                        {
                            Title = p.Title,
                            Route = p.Route,
                            Children = Build(p.Id),
                        })
                        .ToList();

                return Build(null);
            });
        }

        private async Task<string> GenerateUniqueSlugAsync(Page entity)
        {
            var siblings = await GetAsync(p => p.ParentId == entity.ParentId);

            var pattern = @$"^{Regex.Escape(entity.Slug)}(?:-(\d+))?$";

            var takenSuffixes = siblings
                .Where(s => s.Id != entity.Id && Regex.IsMatch(s.Slug, pattern))
                .Select(s =>
                {
                    var match = Regex.Match(s.Slug, pattern);
                    return match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                })
                .ToHashSet();

            // 0 represents the bare slug with no suffix; keep it when available.
            if (!takenSuffixes.Contains(0))
                return entity.Slug;

            int i = 1;

            while (takenSuffixes.Contains(i))
                i++;

            return $"{entity.Slug}-{i}";
        }

        // Walks the ancestor chain to ensure the entity does not become its own ancestor.
        private async Task EnsureNoCycleAsync(Page entity)
        {
            if (entity.Id == Guid.Empty)
                return;

            var parentId = entity.Parent?.Id ?? entity.ParentId;

            while (parentId != null && parentId != Guid.Empty)
            {
                if (parentId == entity.Id)
                    throw new Exception("Tried saving page with circular reference");

                var ancestor = await GetAsync(parentId.Value);

                if (ancestor == null)
                    break;

                parentId = ancestor.ParentId;
            }
        }

        public async Task ChangeParentAsync(Guid childId, Guid parentId)
        {
            var child = await GetAsync(childId);

            if (child.ParentId != parentId)
            {
                var parent = await GetAsync(parentId);

                child.Parent = parent;

                await UpdateAsync(child);
            }
        }

        public async Task UpdateOrderAsync(Guid parentId, IEnumerable<Guid> childOrder)
        {
            var childOrderList = new List<Guid>(childOrder);
            var children = new List<Page>();

            int i = 0;

            children = new List<Page>();

            foreach (var childId in childOrder)
            {
                var child = await GetAsync(childId);

                child.SortOrder = i;

                if (parentId == Guid.Empty)
                    child.Parent = null;

                children.Add(child);

                i++;
            }

            if (parentId == Guid.Empty)
            {
                foreach (var child in children)
                {
                    await UpdateAsync(child);
                }
            }
            else
            {
                var parent = await GetAsync(parentId);

                parent.Children = children;

                await UpdateAsync(parent);
            }
        }

        public async Task FixRoutesAsync()
        {
            await cache.ExpireAsync(MenuCacheKey);

            var rootPages = await GetAsync(p => p.ParentId == null);

            foreach (var page in rootPages)
            {
                page.Parent = null;
                await RefreshRouteTreeAsync(page);
            }
        }

        private async Task RefreshDescendantRoutesAsync(Page page)
        {
            foreach (var child in await GetAsync(p => p.ParentId == page.Id))
            {
                child.Parent = page;
                await RefreshRouteTreeAsync(child);
            }
        }

        private async Task RefreshRouteTreeAsync(Page page)
        {
            var newRoute = RenderRoute(page);

            if (page.Route != newRoute)
            {
                await cache.ExpireAsync(GetCacheKey(page.Route));

                page.Route = newRoute;

                await cache.ExpireAsync(GetCacheKey(page.Route));

                await base.UpdateAsync(page, null);
            }

            await RefreshDescendantRoutesAsync(page);
        }

        public static string GetCacheKey(string route)
        {
            var normalized = route.TrimStart('/');

            if (normalized.StartsWith("Pages/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Pages/".Length);

            return $"Page|{normalized.TrimEnd('/').ToLower()}";
        }

        // Cache key for the public sidebar navigation tree.
        public const string MenuCacheKey = "Page|Menu";

        public static string RenderRoute(Page page)
        {
            var parts = new List<string>();

            if (page.Parent != null)
                parts.Add(page.Parent.Route);
            else
                parts.Add("Pages");

            parts.Add(page.Slug);

            return String.Join('/', parts);
        }
    }
}
