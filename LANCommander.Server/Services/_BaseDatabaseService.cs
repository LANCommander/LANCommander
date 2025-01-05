using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LANCommander.Server.Services
{
    public abstract class BaseDatabaseService<T>(ILogger logger, DatabaseContext dbContext) : BaseService(logger) where T : BaseModel
    {
        protected readonly DatabaseContext Context = dbContext;

        public virtual async Task<ICollection<T>> Get()
        {
            return await Get(x => true).ToListAsync();
        }

        public virtual async Task<T> Get(Guid id)
        {
            return await Context.Set<T>().FindAsync(id);
        }

        public virtual IQueryable<T> Get(Expression<Func<T, bool>> predicate)
        {
            return Context.Set<T>().Where(predicate);
        }

        public virtual bool Exists(Guid id)
        {
            return Get(id) != null;
        }

        public virtual async Task<T> Add(T entity)
        {
            Context.Set<T>().Add(entity);
            await Context.SaveChangesAsync();

            return entity;
        }

        /// <summary>
        /// Adds an entity to the database if it does exist as dictated by the predicate
        /// </summary>
        /// <param name="predicate">Qualifier expression</param>
        /// <param name="entity">Entity to add</param>
        /// <returns>Newly created or existing entity</returns>
        public virtual async Task<ExistingEntityResult<T>> AddMissing(Expression<Func<T, bool>> predicate, T entity)
        {
            var existing = await Context.Set<T>().FirstOrDefaultAsync(predicate);

            if (existing == null)
            {
                Context.Set<T>().Add(entity);

                await Context.SaveChangesAsync();

                return new ExistingEntityResult<T>
                {
                    Value = entity,
                    Existing = false
                };
            }
            else
            {
                return new ExistingEntityResult<T>
                {
                    Value = existing,
                    Existing = true
                };
            }
        }

        public virtual async Task<T> Update(T entity)
        {
            var existing = await Context.Set<T>().FindAsync(entity.Id);

            if (existing == null)
                throw new InvalidOperationException("Attempted to update an entity that does not exist in the database.");

            Context.Entry(existing).CurrentValues.SetValues(entity);

            Context.Set<T>().Update(existing);
            await Context.SaveChangesAsync();

            return entity;
        }

        public virtual async Task Delete(T entity)
        {
            Context.Set<T>().Remove(entity);
            await Context.SaveChangesAsync();
        }
    }
}
