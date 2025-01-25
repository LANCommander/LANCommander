using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public abstract class BaseDatabaseService<T> : BaseService, IBaseDatabaseService<T> where T : class, IBaseModel
    {
        protected readonly IFusionCache Cache;
        protected Repository<T> Repository { get; set; }

        public BaseDatabaseService(
            ILogger logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger)
        {
            Cache = cache;
            Repository = repositoryFactory.Create<T>();
        }

        public IBaseDatabaseService<T> Query(Func<IQueryable<T>, IQueryable<T>> modifier)
        {
            Repository.Query(modifier);

            return this;
        }

        public IBaseDatabaseService<T> Include(params Expression<Func<T, object>>[] expressions)
        {
            Repository.Include(expressions);

            return this;
        }

        public IBaseDatabaseService<T> SortBy(Expression<Func<T, object>> expression, SortDirection direction = SortDirection.Ascending)
        {
            Repository.SortBy(expression, direction);

            return this;
        }

        public IBaseDatabaseService<T> DisableTracking()
        {
            Repository.AsNoTracking();

            return this;
        }

        public async Task<PaginatedResults<T>> PaginateAsync(Expression<Func<T, bool>> expression, int pageNumber, int pageSize)
        {
            return await Repository.PaginateAsync(expression, pageNumber, pageSize);
        }

        public virtual async Task<ICollection<T>> GetAsync()
        {
            return await GetAsync(x => true);
        }

        public virtual async Task<ICollection<U>> GetAsync<U>()
        {
            return await GetAsync<U>(x => true);
        }

        public virtual async Task<T> GetAsync(Guid id)
        {
            return await Repository.FindAsync(id);
        }

        public virtual async Task<U> GetAsync<U>(Guid id)
        {
            return await Repository.FindAsync<U>(id);
        }

        public virtual async Task<ICollection<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            var results = await Repository.GetAsync(predicate);
            
            return results;
        }

        public virtual async Task<ICollection<U>> GetAsync<U>(Expression<Func<T, bool>> predicate)
        {
            var results = await Repository.GetAsync<U>(predicate);
            
            return results;
        }

        public virtual async Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FirstAsync(predicate);
        }

        public virtual async Task<U> FirstAsync<U>(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FirstAsync<U>(predicate);
        }

        public virtual async Task<T> FirstAsync<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await Repository.FirstAsync<TKey>(predicate, orderKeySelector);
        }

        public virtual async Task<U> FirstAsync<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            return await Repository.FirstAsync<U, TKey>(predicate, orderKeySelector);
        }

        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<U> FirstOrDefaultAsync<U>(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FirstOrDefaultAsync<U>(predicate);
        }

        public virtual async Task<T> FirstOrDefaultAsync<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await Repository.FirstOrDefaultAsync<TKey>(predicate, orderKeySelector);
        }

        public virtual async Task<U> FirstOrDefaultAsync<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            return await Repository.FirstOrDefaultAsync<U, TKey>(predicate, orderKeySelector);
        }

        public virtual async Task<bool> ExistsAsync(Guid id)
        {
            return (await FirstOrDefaultAsync(x => x.Id == id)) != null;
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return (await GetAsync(predicate)) != null;
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            entity = await Repository.AddAsync(entity);
            await Repository.SaveChangesAsync();

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
            var existing = await Repository.FirstOrDefaultAsync(predicate);

            if (existing == null)
            {
                await Cache.ExpireAsync($"{typeof(T).FullName}:Get");

                entity = await Repository.AddAsync(entity);

                await Repository.SaveChangesAsync();

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

        public virtual async Task<T> UpdateAsync(T entity)
        {
            entity = await Repository.UpdateAsync(entity);

            await Repository.SaveChangesAsync();

            return entity;
        }

        public virtual async Task DeleteAsync(T entity)
        {
            await Cache.ExpireAsync($"{typeof(T).FullName}:Get");

            Repository.Delete(entity);
            await Repository.SaveChangesAsync();
        }

        public void Dispose()
        {
            /*if (Repository != null)
            {
                Repository.Dispose();
                Repository = null;
            }*/
        }
    }
}
