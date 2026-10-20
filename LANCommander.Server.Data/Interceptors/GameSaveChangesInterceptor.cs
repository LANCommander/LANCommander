using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Data.Interceptors
{
    public class GameSaveChangesInterceptor : SaveChangesInterceptor
    {
        private void EnforceBusinessRules(DbContext? context)
        {
            if (context == null)
                return;

            foreach (var entry in context.ChangeTracker.Entries<Game>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var game = entry.Entity;

                    // Main games have no base game, get their own directory, and are always listed
                    if (game.Type == GameType.MainGame)
                    {
                        game.BaseGame = null;
                        game.BaseGameId = null;
                        game.InstallTo = GameInstallLocation.OwnDirectory;
                        game.ShowInLibrary = true;
                    }
                    // prevent recursion, referencing itself as base
                    else if (game.BaseGameId == game.Id)
                    {
                        game.BaseGame = null;
                        game.BaseGameId = null;
                    }
                }
            }
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            EnforceBusinessRules(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            EnforceBusinessRules(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
