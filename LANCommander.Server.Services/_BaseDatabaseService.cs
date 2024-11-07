using LANCommander.Server.Data;
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
        public Repository<T> Repository { get; set; }

        public BaseDatabaseService(ILogger logger, IFusionCache cache, Repository<T> repository) : base(logger)
        {
            Cache = cache;
            Repository = repository;
        }

        public virtual async Task<ICollection<T>> Get()
        {
            return await Cache.GetOrSetAsync($"{typeof(T).FullName}:Get", async _ =>
            {
                return await Get(x => true);
            }, TimeSpan.FromSeconds(30));
        }

        public virtual async Task<T> Get(Guid id)
        {
            return await Repository.Find(id);
        }

        public virtual async Task<ICollection<T>> Get(Expression<Func<T, bool>> predicate)
        {
            Logger?.LogDebug("Getting data from context ID {ContextId}", Repository.Context.ContextId);
            var results = await Repository.Get(predicate);
            Logger?.LogDebug("Done getting data from context ID {ContextId}", Repository.Context.ContextId);
            return results;
        }

        public virtual async Task<T> First(Expression<Func<T, bool>> predicate)
        {
            return await Repository.First(predicate);
        }

        public virtual async Task<T> First<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await Repository.First(predicate, orderKeySelector);
        }

        public virtual async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            return await Repository.FirstOrDefault(predicate);
        }

        public virtual async Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await Repository.FirstOrDefault(predicate, orderKeySelector);
        }

        public virtual async Task<bool> Exists(Guid id)
        {
            return Get(id) != null;
        }

        public virtual async Task<T> Add(T entity)
        {
            entity = await Repository.Add(entity);
            await Repository.SaveChanges();

            return entity;
        }

        /// <summary>
        /// Adds an entity to the database if it does exist as dictated by the predicate
        /// </summary>
        /// <param name="predicate">Qualifier expressoin</param>
        /// <param name="entity">Entity to add</param>
        /// <returns>Newly created or existing entity</returns>
        public virtual async Task<ExistingEntityResult<T>> AddMissing(Expression<Func<T, bool>> predicate, T entity)
        {
            var existing = await Repository.FirstOrDefault(predicate);

            if (existing == null)
            {
                await Cache.ExpireAsync($"{typeof(T).FullName}:Get");

                entity = await Repository.Add(entity);

                await Repository.SaveChanges();

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
            await Cache.ExpireAsync($"{typeof(T).FullName}:Get");

            entity = await Repository.Update(entity);

            await Repository.SaveChanges();

            return entity;
        }

        public virtual async Task Delete(T entity)
        {
            await Cache.ExpireAsync($"{typeof(T).FullName}:Get");

            Repository.Delete(entity);
            await Repository.SaveChanges();
        }
    }
}
