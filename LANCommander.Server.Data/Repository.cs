using LANCommander.SDK.Extensions;
using LANCommander.SDK;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Threading;

namespace LANCommander.Server.Data
{
    public class Repository<T> : IDisposable where T : class, IBaseModel
    {
        public readonly DatabaseContext Context;
        private readonly IHttpContextAccessor HttpContextAccessor;
        private readonly ILogger Logger;
        private readonly SemaphoreSlim Semaphore = new(1);

        private User User;

        public Repository(IDbContextFactory<DatabaseContext> contextFactory, IHttpContextAccessor httpContextAccessor, ILogger<Repository<T>> logger)
        {
            Context = contextFactory.CreateDbContext();
            HttpContextAccessor = httpContextAccessor;
            Logger = logger;

            Logger?.LogDebug("Opened up context {ContextId}", Context.ContextId);
        }

        private DbSet<T> DbSet
        {
            get { return Context.Set<T>(); }
        }

        private DbSet<User> UserDbSet
        {
            get { return Context.Set<User>(); }
        }

        private IQueryable<T> Query(Expression<Func<T, bool>> predicate)
        {
            using (var op = Logger.BeginOperation("Querying database"))
            {
                var queryable = DbSet.AsQueryable().Where(predicate);

                op.Complete();

                return queryable;
            }
        }

        public async Task <ICollection<T>> Get(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Semaphore.WaitAsync();

                return await Query(predicate).ToListAsync();
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> First(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Semaphore.WaitAsync();

                return await Query(predicate).FirstAsync();
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> First<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Semaphore.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> Find(Guid id)
        {
            try {
                await Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                {
                    var entity = await DbSet.FindAsync(id);

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                {
                    var entity = await Query(predicate).FirstOrDefaultAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Semaphore.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> Add(T entity)
        {
            try
            {
                await Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Adding entity of type {EntityType}", typeof(T).Name))
                {
                    entity.CreatedById = await GetCurrentUserId();
                    entity.UpdatedById = await GetCurrentUserId();
                    entity.CreatedOn = DateTime.UtcNow;
                    entity.UpdatedOn = DateTime.UtcNow;

                    await Context.AddAsync(entity);

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task<T> Update(T entity)
        {
            try
            {
                await Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Updating entity with ID {EntityId}", entity.Id))
                {
                    var existing = await Find(entity.Id);

                    Context.Entry(existing).CurrentValues.SetValues(entity);

                    entity.UpdatedById = await GetCurrentUserId();
                    entity.UpdatedOn = DateTime.UtcNow;

                    Context.Update(entity);

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public void Delete(T entity)
        {
            try
            {
                Semaphore.Wait();

                using (var op = Logger.BeginOperation("Deleting entity with ID {EntityId}", entity.Id))
                {
                    Context.Remove(entity);

                    op.Complete();
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public async Task SaveChanges()
        {
            try
            {
                await Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Saving changes!"))
                {
                    await Context.SaveChangesAsync();

                    op.Complete();
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        private async Task<User> GetUser(string username)
        {
            try
            {
                await Semaphore.WaitAsync();

                return await UserDbSet.FirstOrDefaultAsync(u => u.UserName == username);
            }
            finally
            {
                Semaphore.Release();
            }
        }

        private async Task<Guid?> GetCurrentUserId()
        {
            if (HttpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                if (User == null)
                    User = await GetUser(HttpContextAccessor.HttpContext.User.Identity.Name);

                if (User == null)
                    return null;
                else
                    return User.Id;
            }
            else
                return null;
        }

        public void Dispose()
        {
            try
            {
                Semaphore.Release();
                Context.Dispose();
                Logger?.LogDebug("Disposed context {ContextId}", Context.ContextId);
            }
            catch {
                Logger?.LogDebug("Could not dispose context {ContextId}", Context.ContextId);
            }
        }
    }
}
