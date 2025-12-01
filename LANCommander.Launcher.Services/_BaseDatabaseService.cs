using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LANCommander.Launcher.Services
{
    public abstract class BaseDatabaseService<T> : BaseService where T : BaseModel
    {
        protected DatabaseContext Context { get; set; }

        public BaseDatabaseService(DatabaseContext dbContext, ILogger logger) : base(logger)
        {
            Context = dbContext;
        }

        public virtual async Task<ICollection<T>> GetAsync()
        {
            return await Query(x => true).ToListAsync();
        }

        public virtual async Task<T> GetAsync(Guid id)
        {
            return await Context.Set<T>().FindAsync(id);
        }

        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await Context.Set<T>().FirstOrDefaultAsync(predicate);
        }

        public virtual IQueryable<T> Query(Expression<Func<T, bool>> predicate)
        {
            return Context.Set<T>().Where(predicate);
        }

        public virtual async Task<bool> ExistsAsync(Guid id) => await Context.Set<T>().AnyAsync(x => x.Id == id);
        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => await Context.Set<T>().AnyAsync(predicate);

        public virtual async Task<T> AddAsync(T entity)
        {
            var result = await Context.Set<T>().AddAsync(entity);

            entity = result.Entity;
            
            if (Context.Database.CurrentTransaction == null)
                await Context.SaveChangesAsync();

            return entity;
        }

        /// <summary>
        /// Adds an entity to the database if it does exist as dictated by the predicate
        /// </summary>
        /// <param name="predicate">Qualifier expressoin</param>
        /// <param name="entity">Entity to add</param>
        /// <returns>Newly created or existing entity</returns>
        public virtual async Task<ExistingEntityResult<T>> AddMissingAsync(Expression<Func<T, bool>> predicate, T entity)
        {
            var existing = await Query(predicate).FirstOrDefaultAsync();

            if (existing == null)
            {
                entity = await AddAsync(entity);

                return new ExistingEntityResult<T>
                {
                    Value = entity,
                    Existing = false,
                };
            }
            else
            {
                return new ExistingEntityResult<T>
                {
                    Value = entity,
                    Existing = true,
                };
            }
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            var existing = await GetAsync(entity.Id);
            
            Context.Entry(existing).CurrentValues.SetValues(entity);
            
            entity = Context.Update(existing).Entity;
            
            if (Context.Database.CurrentTransaction == null)
                await Context.SaveChangesAsync();

            return entity;
        }

        public virtual async Task SyncRelatedCollectionAsync<T, TChild, U>(
            T entity, Expression<Func<T, ICollection<TChild>>> navigationProperty,
            IEnumerable<U> records,
            Func<TChild, U, bool> matchPredicate,
            Action<TChild, U> updateAction,
            Func<U, TChild> createAction)
        {
            var collection = navigationProperty.Compile().Invoke(entity);

            var matchedChildren = new HashSet<TChild>();

            foreach (var record in records)
            {
                var existingChild = collection.FirstOrDefault(child => matchPredicate(child, record));

                if (existingChild != null)
                {
                    updateAction(existingChild, record);
                    matchedChildren.Add(existingChild);
                }
                else
                {
                    var newChild = createAction(record);
                    collection.Add(newChild);
                    matchedChildren.Add(newChild);
                }
            }
            
            var toDelete = collection.Where(child => !matchedChildren.Contains(child));

            foreach (var child in toDelete)
            {
                collection.Remove(child);
                Context.Remove(child);
            }

            await Context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(T entity)
        {
            Context.Set<T>().Remove(entity);
            
            if (Context.Database.CurrentTransaction == null)
                await Context.SaveChangesAsync();
        }
    }
}
