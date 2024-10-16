using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LANCommander.Server.Services
{
    public abstract class BaseDatabaseService<T> : BaseService where T : class, IBaseModel
    {
        public Repository<T> Repository { get; set; }

        public BaseDatabaseService(ILogger logger, Repository<T> repository) : base(logger)
        {
            Repository = repository;
        }

        public virtual async Task<ICollection<T>> Get()
        {
            return await Get(x => true);
        }

        public virtual async Task<T> Get(Guid id)
        {
            return await Repository.Find(id);
        }

        public virtual async Task<ICollection<T>> Get(Expression<Func<T, bool>> predicate)
        {
            return await Repository.Get(predicate).ToListAsync();
        }

        public virtual async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            return await Repository.Get(predicate).FirstOrDefaultAsync();
        }

        public virtual async Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            return await Repository.Get(predicate).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
        }

        public virtual bool Exists(Guid id)
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
            var existing = Repository.Get(predicate).FirstOrDefault();

            if (existing == null)
            {
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
            entity = await Repository.Update(entity);

            await Repository.SaveChanges();

            return entity;
        }

        public virtual async Task Delete(T entity)
        {
            Repository.Delete(entity);
            await Repository.SaveChanges();
        }
    }
}
