using System.Linq.Expressions;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Launcher.Models;

public class BulkImportBuilder<TModel, TKeyedModel>
    where TModel : BaseModel
    where TKeyedModel : SDK.Models.IKeyedModel
{
    private readonly DatabaseContext _databaseContext;

    private bool _remove = true;
    private ICollection<TModel> _target;
    private IEnumerable<TKeyedModel> _source;
    private TModel _existingTarget;
    private List<Expression<Func<TModel, object>>> _includes = new();
    private List<Action<TModel, TKeyedModel>> _assignActions = new();

    public BulkImportBuilder(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
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

    public BulkImportBuilder<TModel, TKeyedModel> AsNoRemove()
    {
        _remove = false;

        return this;
    }

    public async Task<ICollection<TModel>> ImportAsync()
    {
        var queryable = _databaseContext.Set<TModel>().AsQueryable();

        foreach (var include in _includes)
        {
            queryable = queryable.Include(include);
        }

        foreach (var source in _source)
        {
            _existingTarget = await queryable.FirstOrDefaultAsync(i => i.Id == source.Id);

            if (_existingTarget != null)
            {
                foreach (var assignAction in _assignActions)
                {
                    assignAction(_existingTarget, source);
                }
                
                _databaseContext.Update(_existingTarget);
                
                if (_databaseContext.Database.CurrentTransaction == null)
                    await _databaseContext.SaveChangesAsync();
            }
            else
            {
                var item = (TModel) Activator.CreateInstance(typeof(TModel));

                item.Id = source.Id;

                foreach (var assignAction in _assignActions)
                {
                    assignAction(item, source);
                }
                
                var result = await _databaseContext.Set<TModel>().AddAsync(item);
                
                if (_databaseContext.Database.CurrentTransaction == null)
                    await _databaseContext.SaveChangesAsync();

                _target.Add(result.Entity);
            }
        }

        if (_remove)
        {
            var toRemove = _target.Where(x => !_source.Any(y => y.Id == x.Id)).ToList();

            foreach (var item in toRemove)
            {
                _target.Remove(item);
            }            
        }

        if (_databaseContext.Database.CurrentTransaction == null)
            await _databaseContext.SaveChangesAsync();

        return _target;
    }
}