using JetBrains.Annotations;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using YamlDotNet.Core.Tokens;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class PageService : BaseDatabaseService<Page>
    {
        private readonly IFusionCache Cache;

        public PageService(
            ILogger<PageService> logger,
            DatabaseContext dbContext,
            IFusionCache cache) : base(logger, dbContext)
        {
            Cache = cache;
        }

        public override async Task<Page> Add(Page entity)
        {
            if (entity.Parent != null && entity.Parent.Parent != null && entity.Parent.Parent.Id == entity.Id)
                throw new Exception("Tried creating page with circular reference");

            entity.Slug = entity.Slug.ToUrlSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToUrlSlug();

            entity.Route = RenderRoute(entity);

            await Cache.ExpireAsync("MappedGames");

            return await base.Add(entity);
        }

        public override async Task<Page> Update(Page entity)
        {
            if (entity.Parent != null && entity.Parent.Parent != null && entity.Parent.Parent.Id == entity.Id)
                throw new Exception("Tried updating page with circular reference");

            entity.Slug = entity.Slug.ToUrlSlug();

            if (String.IsNullOrWhiteSpace(entity.Slug))
                entity.Slug = entity.Title.ToUrlSlug();

            entity.Route = RenderRoute(entity);

            await Cache.ExpireAsync($"Page|{entity.Route}");

            return await base.Update(entity);
        }

        public async Task ChangeParent(Guid childId, Guid parentId)
        {
            var child = await Get(childId);

            if (child.ParentId != parentId)
            {
                var parent = await Get(parentId);

                child.Parent = parent;

                await Update(child);
            }
        }

        public async Task UpdateOrder(Guid parentId, IEnumerable<Guid> childOrder)
        {
            var childOrderList = new List<Guid>(childOrder);
            var children = new List<Page>();

            int i = 0;

            children = new List<Page>();

            foreach (var childId in childOrder)
            {
                var child = await Get(childId);

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
                    await Update(child);
                }
            }
            else
            {
                var parent = await Get(parentId);

                parent.Children = children;

                await Update(parent);
            }
        }

        public async Task FixRoutes()
        {
            var parentPages = await Get(p => p.Parent == null).ToListAsync();

            foreach (var page in parentPages)
            {
                await FixRoute(page);
            }
        }

        private async Task FixRoute(Page page)
        {
            page.Route = RenderRoute(page);

            await Update(page);

            foreach (var child in page.Children)
            {
                await FixRoute(child);
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
