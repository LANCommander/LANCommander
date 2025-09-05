using System.Linq;
using System.Linq.Expressions;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Launcher.Models;

public class BulkImportBuilder<TModel, TKeyedModel>
    where TModel : BaseModel, new()
    where TKeyedModel : SDK.Models.IKeyedModel
{
    private readonly DatabaseContext _databaseContext;

    private bool _remove = true;
    private bool _batchMode = false;
    private DbSet<TModel> _dbSet;
    private ICollection<TModel> _target;
    private IEnumerable<TKeyedModel> _source;
    private TModel _existingTarget;
    private List<Expression<Func<TModel, object>>> _includes = new();
    private List<Action<TModel, TKeyedModel>> _assignActions = new();
    private List<Action<TModel, TKeyedModel>> _relationshipActions = new();

    public BulkImportBuilder(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
        _dbSet = _databaseContext.Set<TModel>();
    }

    public BulkImportBuilder<TModel, TKeyedModel> SetTarget(ICollection<TModel> target)
    {
        _target = target;

        return this;
    }
    
    public BulkImportBuilder<TModel, TKeyedModel> UseSource(IEnumerable<TKeyedModel> source)
    {
        _source = source;

        return this;
    }

    public BulkImportBuilder<TModel, TKeyedModel> Include(Expression<Func<TModel, object>> expression)
    {
        _includes.Add(expression);

        return this;
    }

    public BulkImportBuilder<TModel, TKeyedModel> Assign(Action<TModel, TKeyedModel> assignAction)
    {
        _assignActions.Add(assignAction);

        return this;
    }

    public BulkImportBuilder<TModel, TKeyedModel> AssignRelationships(Action<TModel, TKeyedModel> relationshipAction)
    {
        _relationshipActions.Add(relationshipAction);

        return this;
    }

    public BulkImportBuilder<TModel, TKeyedModel> AsNoRemove()
    {
        _remove = false;

        return this;
    }

    public BulkImportBuilder<TModel, TKeyedModel> AsBatch()
    {
        _batchMode = true;

        return this;
    }

    public async Task SaveChangesAsync()
    {
        await _databaseContext.SaveChangesAsync();
    }

    public async Task<ICollection<TModel>> ImportAsync()
    {
        var entitiesToProcess = new List<(TModel entity, TKeyedModel source, bool isNew)>();

        // First pass: prepare all entities and set basic properties
        foreach (var source in _source)
        {
            // Check if entity is already being tracked first
            var trackedEntity = _databaseContext.ChangeTracker.Entries<TModel>()
                .FirstOrDefault(e => e.Entity.Id == source.Id);

            TModel entity;
            bool isNew = false;
            
            if (trackedEntity != null)
            {
                // Use the already tracked entity
                entity = trackedEntity.Entity;
            }
            else
            {
                // Check if entity exists in database
                var exists = await _dbSet.AsNoTracking().AnyAsync(e => e.Id == source.Id);
                
                if (exists)
                {
                    // Load the existing entity from database
                    entity = await _dbSet.FindAsync(source.Id);
                    if (entity == null)
                    {
                        // Fallback: create new entity if FindAsync returns null
                        entity = new TModel { Id = source.Id };
                        _databaseContext.Attach(entity);
                    }
                }
                else
                {
                    // Create new entity
                    entity = new TModel { Id = source.Id };
                    _databaseContext.Attach(entity);
                    _databaseContext.Entry(entity).State = EntityState.Added;
                    isNew = true;
                }
            }
            
            // Execute assign actions to set basic properties (but not relationships yet)
            foreach (var assignAction in _assignActions)
                assignAction(entity, source);
            
            entitiesToProcess.Add((entity, source, isNew));
        }

        // Save entities to ensure they exist in database for relationship establishment
        // In batch mode, this will be the only save operation
        await _databaseContext.SaveChangesAsync();

        // Second pass: establish relationships and add to target collection
        foreach (var (entity, source, isNew) in entitiesToProcess)
        {
            // Execute relationship actions after entities are saved
            foreach (var relationshipAction in _relationshipActions)
                relationshipAction(entity, source);

            // Add to target collection if it's a new entity
            if (isNew)
                _target.Add(entity);
        }

        if (_remove)
        {
            var toRemove = _target.Where(x => !_source.Any(y => y.Id == x.Id)).ToList();

            _databaseContext.RemoveRange(toRemove);
        }

        return _target;
    }
}