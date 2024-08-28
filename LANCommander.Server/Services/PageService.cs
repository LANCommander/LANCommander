using JetBrains.Annotations;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using System.Text;

namespace LANCommander.Server.Services
{
    public class PageService : BaseDatabaseService<Page>
    {
        public PageService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
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

        public static string GetParentRoute(Page page)
        {
            var parts = new List<string>();
            var parent = page.Parent;

            while (parent != null)
            {
                parts.Add(parent.Route);

                parent = parent.Parent;
            }

            return String.Join('/', parts);
        }
    }
}
