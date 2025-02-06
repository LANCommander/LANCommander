using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LANCommander.Server.Data.Interceptors
{
    public sealed class AuditingInterceptor(IHttpContextAccessor? httpContextAccessor) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            var context = eventData.Context as DatabaseContext;

            if (context is null)
            {
                return base.SavingChanges(eventData, result);
            }

            context.ChangeTracker.DetectChanges();

            var entries = context.ChangeTracker.Entries<BaseModel>();

            User? currentUser = GetCurrentUser(context);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedOn = DateTime.Now;
                    entry.Entity.CreatedBy = currentUser;
                }

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedOn = DateTime.Now;
                    entry.Entity.UpdatedBy = currentUser;
                }
            }

            return base.SavingChanges(eventData, result);
        }

        private User? GetCurrentUser(DatabaseContext databaseContext)
        {
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext != null && httpContext.User != null && httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated)
            {
                return GetUser(httpContext.User.Identity?.Name, databaseContext);
            }

            return null;
        }

        private static User? GetUser(string? username, DatabaseContext databaseContext) =>
            databaseContext.Users.FirstOrDefault(u => u.UserName == username);
    }
}