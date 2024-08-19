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
