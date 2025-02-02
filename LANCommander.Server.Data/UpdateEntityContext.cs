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
    
        var updatedEntity = compiledExpression.Invoke(_updatedEntity);

        if (updatedEntity != null)
        {
            var existingEntityEntry = _context.Entry(_entity);
            
            var navigation = existingEntityEntry.Reference(navigationPropertyPath);
            
            await navigation.LoadAsync();
            
            existingEntityEntry.Reference(navigationPropertyPath).CurrentValue = updatedEntity;

            _context.Entry(_entity).State = EntityState.Modified;
        }
    }
    
    public async Task UpdateRelationshipAsync<TRelatedEntity>(Expression<Func<TEntity, ICollection<TRelatedEntity>>> navigationPropertyPath)
        where TRelatedEntity : class, IBaseModel
    {
        var compiledExpression = navigationPropertyPath.Compile();
            
        var updatedCollection = compiledExpression.Invoke(_updatedEntity);

        if (updatedCollection is IEnumerable<TRelatedEntity> updatedEntities)
        {
            var existingCollection = compiledExpression(_entity);

            if (existingCollection is ICollection<TRelatedEntity> existingEntities)
            {
                existingEntities.Clear();
                    
                foreach (var item in updatedEntities)
                    existingEntities.Add(item);
                    
                _context.Entry(_entity).State = EntityState.Modified;
            }
        }
    }
}