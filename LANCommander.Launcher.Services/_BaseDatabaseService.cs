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

        public BaseDatabaseService(DatabaseContext dbContext, SDK.Client client, ILogger logger) : base(client, logger)
        {
            Context = dbContext;
        }

        public virtual async Task<ICollection<T>> Get()
        {
            return await Get(x => true).ToListAsync();
        }

        public virtual async Task<T> Get(Guid id)
        {
            using (var repo = new Repository<T>(Context))
            {
                return await repo.Find(id);
            }
        }

        public virtual IQueryable<T> Get(Expression<Func<T, bool>> predicate)
        {
            using (var repo = new Repository<T>(Context))
            {
                return repo.Get(predicate);
            }
        }

        public virtual bool Exists(Guid id)
        {
            return Get(id) != null;
        }

        public virtual async Task<T> Add(T entity)
        {
            using (var repo = new Repository<T>(Context))
            {
                entity = await repo.Add(entity);
                await repo.SaveChanges();

                return entity;
            }
        }

        /// <summary>
        /// Adds an entity to the database if it does exist as dictated by the predicate
        /// </summary>
        /// <param name="predicate">Qualifier expressoin</param>
        /// <param name="entity">Entity to add</param>
        /// <returns>Newly created or existing entity</returns>
        public virtual async Task<ExistingEntityResult<T>> AddMissing(Expression<Func<T, bool>> predicate, T entity)
        {
            using (var repo = new Repository<T>(Context))
            {
                var existing = repo.Get(predicate).FirstOrDefault();

                if (existing == null)
                {
                    entity = await repo.Add(entity);

                    await repo.SaveChanges();

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
        }

        public virtual async Task<T> Update(T entity)
        {
            using (var repo = new Repository<T>(Context))
            {
                var existing = await repo.Find(entity.Id);

                Context.Entry(existing).CurrentValues.SetValues(entity);

                entity = repo.Update(existing);
                await repo.SaveChanges();

                return entity;
            }
        }

        public virtual async Task Delete(T entity)
        {
            using (var repo = new Repository<T>(Context))
            {
                repo.Delete(entity);
                await repo.SaveChanges();
            }
        }
    }
}
