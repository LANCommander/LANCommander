using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data;

/// <summary>
/// Remembers which collection navigations a query actually loaded for an entity, so the
/// information survives the disposal of the short-lived context that materialized it.
///
/// Models initialize their collection navigations to empty collections, so once an entity is
/// detached there is no way to tell "this game has no archives" apart from "archives were never
/// included in the query". Updates need that distinction: without it, saving an entity that was
/// loaded by a query which omitted a navigation looks like a request to empty that navigation,
/// and the children get orphaned or cascade deleted.
///
/// Entries are keyed on the entity instance itself and are collected along with it, so nothing
/// needs to be cleaned up. Entities that never came from a query (built by hand, deserialized
/// from an API request) have no recorded state, and callers fall back to their previous behavior.
/// </summary>
public static class EntityLoadState
{
    private static readonly ConditionalWeakTable<object, HashSet<string>> LoadedNavigations = new();

    /// <summary>Shared instance for the common case of a query with no includes.</summary>
    private static readonly HashSet<string> Nothing = new(StringComparer.Ordinal);

    /// <summary>
    /// Records the loaded collection navigations of every entity currently tracked by the context.
    /// Call this after materializing a query but before the context is disposed. Entities returned
    /// by a no-tracking query are not tracked and so are intentionally left unrecorded.
    /// </summary>
    public static void Record(DbContext context)
    {
        // Enumerating entries would otherwise run change detection over the whole graph. This only
        // reads which navigations were loaded, and runs after every query, so skip that work.
        var autoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;

        context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity == null)
                    continue;

                HashSet<string>? loaded = null;

                foreach (var collection in entry.Collections)
                {
                    if (!collection.IsLoaded)
                        continue;

                    loaded ??= new HashSet<string>(StringComparer.Ordinal);
                    loaded.Add(collection.Metadata.Name);
                }

                LoadedNavigations.AddOrUpdate(entry.Entity, loaded ?? Nothing);
            }
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    /// <summary>
    /// Returns whether it is known that <paramref name="navigationName"/> was not loaded for
    /// <paramref name="entity"/>. False when the entity has no recorded state, which keeps
    /// entities that never went through a query on their previous, sync-everything behavior.
    /// </summary>
    public static bool IsKnownUnloaded(object entity, string navigationName)
    {
        return LoadedNavigations.TryGetValue(entity, out var loaded) && !loaded.Contains(navigationName);
    }
}
