using LANCommander.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Interceptors
{
    /// <summary>
    /// Ensures the file for an archive is deleted whenever dropped from the database
    /// Also ensures that when games/redistributables are deleted, archives are deleted as well
    /// </summary>
    public class DeleteArchiveInterceptor(
        ArchiveService archiveService,
        IFusionCache cache) : SaveChangesInterceptor
    {
        private readonly List<Archive> _pendingArchives = new();
        
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = new())
        {
            foreach (var archive in _pendingArchives)
            {
                FileHelpers.DeleteIfExists(await archiveService.GetArchiveFileLocationAsync(archive));
                
                await cache.ExpireGameCacheAsync(archive.GameId);
                await cache.ExpireArchiveCacheAsync(archive.Id);
            }
            
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            
            if (context is null)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var deletedGames = context.ChangeTracker
                .Entries<Game>()
                .Where(e => e.State == EntityState.Deleted)
                .Select(e => e.Entity)
                .ToList();

            foreach (var game in deletedGames)
            {
                var archives = context.Entry(game).Collection(g => g.Archives!);

                if (!archives.IsLoaded)
                    await archives.LoadAsync(cancellationToken);

                if (game.Archives != null)
                    foreach (var archive in game.Archives)
                    {
                        var entry = context.Entry(archive);

                        if (entry.State == EntityState.Deleted || entry.State == EntityState.Unchanged)
                            entry.State = EntityState.Deleted;
                    }
            }
            
            var deletedRedistributables = context.ChangeTracker
                .Entries<Redistributable>()
                .Where(e => e.State == EntityState.Deleted)
                .Select(e => e.Entity)
                .ToList();

            foreach (var redistributable in deletedRedistributables)
            {
                var archives = context.Entry(redistributable).Collection(r => r.Archives!);
                
                if (!archives.IsLoaded)
                    await archives.LoadAsync(cancellationToken);

                if (redistributable.Archives != null)
                    foreach (var archive in redistributable.Archives)
                    {
                        var entry = context.Entry(archive);

                        if (entry.State == EntityState.Deleted || entry.State == EntityState.Unchanged)
                            entry.State = EntityState.Deleted;
                    }
            }

            foreach (var entry in context.ChangeTracker.Entries<Archive>())
            {
                var storageLocation = entry.Reference(a => a.StorageLocation);
                
                if (!storageLocation.IsLoaded)
                    await storageLocation.LoadAsync(cancellationToken);
                
                if (entry.State == EntityState.Deleted)
                    _pendingArchives.Add(entry.Entity);
            }
            
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
