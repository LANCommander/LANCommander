using System.Security.Claims;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Data.Interceptors
{
    /// <summary>
    /// Stamps the audit columns on every row being saved, however it was saved: through a service,
    /// directly through a DbContext, or as a child added through a navigation collection.
    /// <list type="bullet">
    /// <item>Inserted rows get CreatedById and CreatedOn when they don't have them yet.</item>
    /// <item>Updated rows get UpdatedById and UpdatedOn, unless the caller set them in this save
    /// (importers carry over the exported editor; some services stamp their own).</item>
    /// </list>
    /// The user is the signed-in user of the current request or Blazor circuit. Rows saved with no
    /// signed-in user (startup, background jobs) get the dates but no user.
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        const string CreatedById = nameof(BaseModel.CreatedById);
        const string CreatedBy = nameof(BaseModel.CreatedBy);
        const string CreatedOn = nameof(BaseModel.CreatedOn);
        const string UpdatedById = nameof(BaseModel.UpdatedById);
        const string UpdatedOn = nameof(BaseModel.UpdatedOn);

        /// <summary>HttpContext.Items key caching whether the signed-in user is a local user.</summary>
        static readonly object VerifiedUserKey = new();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is { } context)
            {
                var pending = PendingEntries(context);

                if (pending.Count > 0)
                {
                    var (httpContext, claimedId) = GetClaimedUser(context);

                    Stamp(pending, Verify(httpContext, claimedId, id => context.Set<User>().Any(u => u.Id == id)));
                }
            }

            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context)
            {
                var pending = PendingEntries(context);

                if (pending.Count > 0)
                {
                    var (httpContext, claimedId) = GetClaimedUser(context);

                    Guid? userId = null;

                    if (claimedId != null)
                    {
                        if (httpContext?.Items[VerifiedUserKey] is bool known)
                            userId = known ? claimedId : null;
                        else
                        {
                            var exists = await context.Set<User>().AnyAsync(u => u.Id == claimedId, cancellationToken);

                            userId = Remember(httpContext, claimedId.Value, exists);
                        }
                    }

                    Stamp(pending, userId);
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>Inserted or updated rows that have audit columns.</summary>
        static List<EntityEntry> PendingEntries(DbContext context)
        {
            // Interceptors run before SaveChanges detects changes, so children added only through a
            // navigation collection (or edited without the context noticing) wouldn't show up yet
            if (context.ChangeTracker.AutoDetectChangesEnabled)
                context.ChangeTracker.DetectChanges();

            return context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added && (NeedsCreator(e) || NeedsCreatedOn(e))
                         || e.State == EntityState.Modified && HasUpdateColumns(e))
                .ToList();
        }

        static bool NeedsCreator(EntityEntry entry)
        {
            if (entry.Metadata.FindProperty(CreatedById) is not { } property)
                return false;

            if (entry.Property(property.Name).CurrentValue != null)
                return false;

            // A creator assigned through the navigation is fixed up to the key on save
            return entry.Metadata.FindNavigation(CreatedBy) is not { } navigation
                || entry.Navigation(navigation.Name).CurrentValue == null;
        }

        static bool NeedsCreatedOn(EntityEntry entry) =>
            entry.Metadata.FindProperty(CreatedOn) is { } property
            && entry.Property(property.Name).CurrentValue is DateTime createdOn
            && createdOn == default;

        static bool HasUpdateColumns(EntityEntry entry) =>
            entry.Metadata.FindProperty(UpdatedById) != null || entry.Metadata.FindProperty(UpdatedOn) != null;

        static void Stamp(List<EntityEntry> entries, Guid? userId)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (userId != null && NeedsCreator(entry))
                        entry.Property(CreatedById).CurrentValue = userId;

                    if (NeedsCreatedOn(entry))
                        entry.Property(CreatedOn).CurrentValue = now;
                }
                else
                {
                    // Values the caller set in this save win; an UpdatedBy navigation is fixed up to
                    // UpdatedById by DetectChanges, so it shows as a modified key here too
                    if (userId != null
                        && entry.Metadata.FindProperty(UpdatedById) != null
                        && entry.Property(UpdatedById) is { IsModified: false } updatedBy)
                        updatedBy.CurrentValue = userId;

                    if (entry.Metadata.FindProperty(UpdatedOn) != null
                        && entry.Property(UpdatedOn) is { IsModified: false } updatedOn)
                        updatedOn.CurrentValue = now;
                }
            }
        }

        static Guid? Verify(HttpContext? httpContext, Guid? claimedId, Func<Guid, bool> exists)
        {
            if (claimedId == null)
                return null;

            if (httpContext?.Items[VerifiedUserKey] is bool known)
                return known ? claimedId : null;

            return Remember(httpContext, claimedId.Value, exists(claimedId.Value));
        }

        static Guid? Remember(HttpContext? httpContext, Guid claimedId, bool exists)
        {
            // Only a confirmed user is cached: a user created partway through a request still counts
            if (exists && httpContext != null)
                httpContext.Items[VerifiedUserKey] = true;

            return exists ? claimedId : null;
        }

        /// <summary>
        /// The signed-in user's id from the current HttpContext (which also flows into Blazor circuits).
        /// Callers check it against the Users table before use, since an id that isn't a local user
        /// would fail the foreign key and the whole save with it.
        /// </summary>
        static (HttpContext? HttpContext, Guid? UserId) GetClaimedUser(DbContext context)
        {
            var services = context.GetService<IDbContextOptions>()
                .FindExtension<CoreOptionsExtension>()?
                .ApplicationServiceProvider;

            var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
            var user = httpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return (httpContext, null);

            return (httpContext, Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
        }
    }
}
