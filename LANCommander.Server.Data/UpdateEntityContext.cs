using System.Linq.Expressions;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data;

public class UpdateEntityContext<TEntity>
    where TEntity : class, IBaseModel
{
    private readonly DatabaseContext _context;
    private readonly TEntity _entity;
    private readonly TEntity _updatedEntity;

    public UpdateEntityContext(DatabaseContext context, TEntity entity, TEntity updatedEntity)
    {
        _context = context;
        _entity = entity;
        _updatedEntity = updatedEntity;
    }

    public async Task UpdateRelationshipAsync<TRelatedEntity>(
        Expression<Func<TEntity, TRelatedEntity?>> navigationPropertyPath)
        where TRelatedEntity : class, IBaseModel
    {
        var compiledExpression = navigationPropertyPath.Compile();

        // Get the updated entity from the new entity
        var relatedEntity = compiledExpression.Invoke(_updatedEntity);

        if (relatedEntity != null)
        {
            var existingEntityEntry = _context.Entry(_entity);
            var navigation = existingEntityEntry.Reference(navigationPropertyPath);

            // Ensure the existing entity is loaded if needed
            if (!navigation.IsLoaded)
            {
                await navigation.LoadAsync();
            }

            // Check if the related entity is already being tracked
            var trackedRelatedEntity = _context.Set<TRelatedEntity>().Local
                .FirstOrDefault(e => e.Id == relatedEntity.Id);

            if (trackedRelatedEntity != null)
            {
                // Use the already tracked instance to avoid duplicate tracking
                navigation.CurrentValue = trackedRelatedEntity;
            }
            else
            {
                // Fetch the related entity from the database (ensuring EF Core tracks it)
                var existingRelatedEntity = await _context.Set<TRelatedEntity>()
                    .FirstOrDefaultAsync(e => e.Id == relatedEntity.Id);

                if (existingRelatedEntity != null)
                {
                    navigation.CurrentValue = existingRelatedEntity; // Use the tracked instance
                }
                else
                {
                    // If the entity is truly new and not in the DB, attach it
                    _context.Attach(relatedEntity);
                    navigation.CurrentValue = relatedEntity;
                }
            }

            _context.Entry(_entity).State = EntityState.Modified;
        }
    }
    
    public async Task UpdateRelationshipAsync<TRelatedEntity>(
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationPropertyPath)
        where TRelatedEntity : class, IBaseModel
    {
        var compiledExpression = navigationPropertyPath.Compile();

        // Explicitly load the existing collection
        var navigation = _context.Entry(_entity).Collection(navigationPropertyPath);
        
        if (!navigation.IsLoaded)
            await navigation.LoadAsync();

        // Get the updated collection from the new entity
        var updatedCollection = compiledExpression.Invoke(_updatedEntity);

        if (updatedCollection == null)
            return;

        if (updatedCollection is IEnumerable<TRelatedEntity> updatedEntities)
        {
            var existingCollection = compiledExpression(_entity);

            if (existingCollection is ICollection<TRelatedEntity> existingEntities)
            {
                // Get the list of tracked entities from the context
                var trackedEntities = _context.Set<TRelatedEntity>().Local;

                // Replace entities with tracked instances to avoid duplicate tracking
                var updatedTrackedEntities = updatedEntities
                    .Select(e => trackedEntities.FirstOrDefault(t => t.Id == e.Id) ?? e)
                    .ToList();

                // Update values for existing entities first
                foreach (var existingEntity in existingEntities)
                {
                    var updatedEntity = updatedEntities.FirstOrDefault(e => e.Id == existingEntity.Id);
                    if (updatedEntity != null)
                    {
                        _context.Entry(existingEntity).CurrentValues.SetValues(updatedEntity);
                    }
                }

                // Clear the existing collection and add all updated entities
                existingEntities.Clear();
                foreach (var entity in updatedTrackedEntities)
                {
                    existingEntities.Add(entity);
                }

                _context.Entry(_entity).State = EntityState.Modified;
            }
        }
    }

}