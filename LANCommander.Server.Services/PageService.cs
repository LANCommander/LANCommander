using JetBrains.Annotations;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using YamlDotNet.Core.Tokens;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PageService(
        ILogger<PageService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Page>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Page> AddAsync(Page entity)
        {
            if (entity.Parent != null && entity.Parent.Parent != null && entity.Parent.Parent.Id == entity.Id)
                throw new Exception("Tried creating page with circular reference");

            entity.Slug = entity.Slug.ToUrlSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToUrlSlug();

            entity.Route = RenderRoute(entity);

            int i = 1;

            // Fetch all siblings with the same ParentId
            var siblings = await GetAsync(p => p.ParentId == entity.ParentId);

            // Filter siblings that match the slug pattern
            var matchingSiblings = siblings
                .Where(s => Regex.IsMatch(s.Slug, @$"^{Regex.Escape(entity.Slug)}(?:-(\d+))?$"))
                .Select(s =>
                {
                    // Extract numeric suffix, or use 0 for the base slug
                    var match = Regex.Match(s.Slug, @$"^{Regex.Escape(entity.Slug)}(?:-(\d+))?$");
                    return match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                })
                .ToList();

            // Find the next available number
            while (matchingSiblings.Contains(i))
            {
                i++;
            }

            // Set the new slug and route
            entity.Slug = $"{entity.Slug}-{i}";
            entity.Route = RenderRoute(entity);

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
            if (entity.Parent != null && entity.Parent.Parent != null && entity.Parent.Parent.Id == entity.Id)
                throw new Exception("Tried updating page with circular reference");

            entity.Slug = entity.Slug.ToUrlSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToUrlSlug();

            entity.Route = RenderRoute(entity);

            await cache.ExpireAsync($"Page/{entity.Route}");

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.Children);
                await context.UpdateRelationshipAsync(p => p.Games);
                await context.UpdateRelationshipAsync(p => p.Parent);
                await context.UpdateRelationshipAsync(p => p.Redistributables);
                await context.UpdateRelationshipAsync(p => p.Servers);
            });
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
            var parentPages = await GetAsync(p => p.Parent == null);

            foreach (var page in parentPages)
            {
                await FixRouteAsync(page);
            }
        }

        private async Task FixRouteAsync(Page page)
        {
            page.Route = RenderRoute(page);

            await UpdateAsync(page);

            foreach (var child in page.Children)
            {
                await FixRouteAsync(child);
            }
        }

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
