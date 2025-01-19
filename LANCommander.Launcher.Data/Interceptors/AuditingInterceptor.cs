using System.Security.Claims;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LANCommander.Launcher.Data.Interceptors
{
    public sealed class AuditingInterceptor : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;

            if (context is null)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            context.ChangeTracker.DetectChanges();

            var entries = context.ChangeTracker.Entries<BaseModel>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedOn = DateTime.UtcNow;

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    entry.Entity.UpdatedOn = DateTime.UtcNow;
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
