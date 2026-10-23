using LANCommander.Launcher.Data;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Exposes the launcher's database to scripts through <see cref="ILocalLibraryProvider"/>. Scripts
/// are built from the root provider and run on their own threads, so each call opens its own scope
/// rather than sharing a <see cref="DatabaseContext"/> with whatever started the script.
/// </summary>
public class LocalLibraryProvider(IServiceScopeFactory scopeFactory) : ILocalLibraryProvider
{
    public async Task<IReadOnlyList<LocalGame>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        return await context.Games
            .AsNoTracking()
            .OrderBy(g => g.SortTitle ?? g.Title)
            .Select(g => new LocalGame
            {
                Id = g.Id,
                Title = g.Title,
                SortTitle = g.SortTitle,
                Type = g.Type,
                BaseGameId = g.BaseGameId,
                Installed = g.Installed,
                InstallDirectory = g.InstallDirectory,
                InstalledVersion = g.InstalledVersion,
                InstalledVersionId = g.InstalledVersionId,
                InstalledOn = g.InstalledOn,
                LatestVersion = g.LatestVersion,
            })
            .ToListAsync(cancellationToken);
    }
}
