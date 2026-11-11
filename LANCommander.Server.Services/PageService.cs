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
        IDbContextFactory<DatabaseContext> contextFactory,
        MediaService mediaService) : BaseDatabaseService<Page>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        // Root-level slugs that would be shadowed by the site's own routes under /Pages
        static readonly string[] ReservedRootSlugs = ["Edit", "Search", "Appearance"];

        // Characters of context kept either side of a search match in its snippet
        const int SnippetContext = 80;

        // Matches media references such as /api/Media/{id}/Download in page contents
        static readonly Regex MediaReference = new(@"/api/Media/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Uploaded page images that were never saved into a page are only swept after this
        // long, so an image uploaded into an editor that hasn't been saved yet survives.
        static readonly TimeSpan AbandonedImageGracePeriod = TimeSpan.FromDays(1);

        public override async Task<Page> AddAsync(Page entity)
        {
            await ResolveParentAsync(entity);
            await EnsureNoCycleAsync(entity);

            if (entity.SortOrder == 0)
                entity.SortOrder = await GetNextSortOrderAsync(entity.ParentId);

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
            await ResolveParentAsync(entity);
            await EnsureNoCycleAsync(entity);

            var previousContents = (await FirstOrDefaultAsync(p => p.Id == entity.Id))?.Contents;

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

            await CleanUpImagesAsync(GetMediaReferences(previousContents).Except(GetMediaReferences(entity.Contents)));

            return updated;
        }

        public override async Task DeleteAsync(Page entity)
        {
            // Deleting a page deletes everything beneath it. The whole subtree is removed explicitly
            // rather than left to the database cascade so the cached renders and images go too.
            var allPages = await GetAsync();
            var deleted = GetSubtree(allPages, entity.Id).ToList();

            if (deleted.Count == 0)
                deleted.Add(entity);

            foreach (var page in deleted)
                await cache.ExpireAsync(GetCacheKey(page.Route));

            await cache.ExpireAsync(MenuCacheKey);

            await base.DeleteRangeAsync(deleted);

            await CleanUpImagesAsync(deleted.SelectMany(p => GetMediaReferences(p.Contents)));
        }

        /// <summary>
        /// Counts every page beneath the given page, all of which a delete would also remove
        /// </summary>
        public async Task<int> CountDescendantsAsync(Guid pageId)
        {
            var allPages = await GetAsync();

            return Math.Max(0, GetSubtree(allPages, pageId).Count() - 1);
        }

        static IEnumerable<Page> GetSubtree(ICollection<Page> allPages, Guid rootId)
        {
            var childrenByParent = allPages.ToLookup(p => p.ParentId);
            var root = allPages.FirstOrDefault(p => p.Id == rootId);

            if (root == null)
                yield break;

            var pending = new Stack<Page>([root]);

            while (pending.Count > 0)
            {
                var page = pending.Pop();

                yield return page;

                foreach (var child in childrenByParent[page.Id])
                    pending.Push(child);
            }
        }

        public static IEnumerable<Guid> GetMediaReferences(string? contents)
        {
            if (String.IsNullOrEmpty(contents))
                return [];

            return MediaReference.Matches(contents)
                .Select(m => Guid.Parse(m.Groups[1].Value))
                .Distinct();
        }

        /// <summary>
        /// Deletes page images no page references anymore: the candidates given (just removed
        /// from a page) right away, and any other unreferenced page image once it is old enough
        /// to be an abandoned upload rather than one waiting on an unsaved edit.
        /// </summary>
        async Task CleanUpImagesAsync(IEnumerable<Guid> candidates)
        {
            try
            {
                var removed = candidates.Distinct().ToList();
                var abandonedBefore = DateTime.UtcNow - AbandonedImageGracePeriod;

                // The site's header, footer and logo can use uploaded page images too
                var appearance = _settingsProvider.CurrentValue.Server.Pages;

                var referenced = (await GetAsync())
                    .Select(p => p.Contents)
                    .Append(appearance.Logo)
                    .Append(appearance.Header)
                    .Append(appearance.Footer)
                    .SelectMany(GetMediaReferences)
                    .ToHashSet();

                var candidateImages = await mediaService.GetAsync(m => m.Type == SDK.Enums.MediaType.PageImage
                        && (removed.Contains(m.Id) || m.CreatedOn < abandonedBefore));

                var orphans = candidateImages.Where(m => !referenced.Contains(m.Id)).ToList();

                if (orphans.Count > 0)
                    await mediaService.DeleteRangeAsync(orphans);
            }
            catch (Exception ex)
            {
                // The page itself saved fine; leftover images only waste space
                _logger?.LogError(ex, "Could not clean up unused page images");
            }
        }

        /// <summary>
        /// Finds pages containing every word of the query in their title or text, best matches
        /// first: whole-query title matches, then title words, then how often the words appear.
        /// </summary>
        public async Task<IReadOnlyList<Pages.PageSearchResult>> SearchAsync(string? query, int limit = 50)
        {
            var terms = (query ?? "")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (terms.Length == 0)
                return [];

            var allPages = await GetAsync();
            var pagesById = allPages.ToDictionary(p => p.Id);
            var results = new List<(Pages.PageSearchResult Result, int Score)>();

            foreach (var page in allPages)
            {
                var title = page.Title ?? "";
                var text = Pages.PageMarkdown.ToPlainText(page.Contents);

                if (!terms.All(t => title.Contains(t, StringComparison.OrdinalIgnoreCase) || text.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var score = 0;

                if (title.Contains(query!.Trim(), StringComparison.OrdinalIgnoreCase))
                    score += 100;

                score += 10 * terms.Count(t => title.Contains(t, StringComparison.OrdinalIgnoreCase));
                score += terms.Sum(t => Math.Min(10, CountOccurrences(text, t)));

                results.Add((new Pages.PageSearchResult
                {
                    Id = page.Id,
                    Title = title,
                    Route = page.Route,
                    Path = GetAncestorTitles(page, pagesById),
                    Snippet = GetSnippet(text, terms),
                }, score));
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Result.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(r => r.Result)
                .ToList();
        }

        static int CountOccurrences(string text, string term)
        {
            var count = 0;
            var index = 0;

            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += term.Length;
            }

            return count;
        }

        static IReadOnlyList<string> GetAncestorTitles(Page page, Dictionary<Guid, Page> pagesById)
        {
            var titles = new List<string>();
            var parentId = page.ParentId;

            // Bounded in case of a corrupt cycle
            while (parentId is Guid id && pagesById.TryGetValue(id, out var parent) && titles.Count < pagesById.Count)
            {
                titles.Insert(0, parent.Title);
                parentId = parent.ParentId;
            }

            return titles;
        }

        /// <summary>
        /// A short excerpt around the earliest match, or the start of the text if only the title matched
        /// </summary>
        public static string GetSnippet(string text, IEnumerable<string> terms)
        {
            if (String.IsNullOrEmpty(text))
                return "";

            var first = terms
                .Select(t => text.IndexOf(t, StringComparison.OrdinalIgnoreCase))
                .Where(i => i >= 0)
                .DefaultIfEmpty(0)
                .Min();

            var start = Math.Max(0, first - SnippetContext);
            var end = Math.Min(text.Length, first + SnippetContext * 2);

            // Don't cut words in half at either end
            if (start > 0)
            {
                var space = text.IndexOf(' ', start);

                if (space >= 0 && space < first)
                    start = space + 1;
            }

            if (end < text.Length)
            {
                var space = text.LastIndexOf(' ', end);

                if (space > first)
                    end = space;
            }

            return (start > 0 ? "…" : "") + text[start..end].Trim() + (end < text.Length ? "…" : "");
        }

        /// <summary>
        /// Copies a page's title, contents and links into a new page beside it. Child pages aren't copied.
        /// </summary>
        public async Task<Page> DuplicateAsync(Guid id)
        {
            var original = await Include(p => p.Games, p => p.Redistributables, p => p.Servers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (original == null)
                throw new InvalidOperationException("The page to duplicate no longer exists");

            return await AddAsync(new Page
            {
                Title = $"{original.Title} (Copy)",
                Slug = "",
                Contents = original.Contents,
                ParentId = original.ParentId,
                Games = original.Games ?? [],
                Redistributables = original.Redistributables ?? [],
                Servers = original.Servers ?? [],
            });
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

        /// <summary>
        /// Makes Parent and ParentId agree. A set Parent wins (callers reparent by assigning it);
        /// otherwise the parent is loaded from ParentId so the route renders beneath it.
        /// </summary>
        private async Task ResolveParentAsync(Page entity)
        {
            if (entity.Parent != null)
                entity.ParentId = entity.Parent.Id;
            else if (entity.ParentId is Guid parentId && parentId != Guid.Empty)
                entity.Parent = await FirstOrDefaultAsync(p => p.Id == parentId);

            if (entity.Parent == null)
                entity.ParentId = null;
        }

        private async Task<int> GetNextSortOrderAsync(Guid? parentId)
        {
            var siblings = await GetAsync(p => p.ParentId == parentId);

            return siblings.Count == 0 ? 0 : siblings.Max(p => p.SortOrder) + 1;
        }

        private async Task<string> GenerateUniqueSlugAsync(Page entity)
        {
            var siblings = await GetAsync(p => p.ParentId == entity.ParentId);

            // Routes resolve case-insensitively, so slugs differing only by case collide
            var pattern = new Regex(@$"^{Regex.Escape(entity.Slug)}(?:-(\d+))?$", RegexOptions.IgnoreCase);

            var taken = siblings
                .Where(s => s.Id != entity.Id)
                .Select(s => s.Slug);

            if (entity.ParentId == null)
                taken = taken.Concat(ReservedRootSlugs);

            var takenSuffixes = taken
                .Select(slug => pattern.Match(slug))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0)
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
                child.ParentId = parent.Id;
                child.SortOrder = await GetNextSortOrderAsync(parent.Id);

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
                {
                    child.Parent = null;
                    child.ParentId = null;
                }

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
