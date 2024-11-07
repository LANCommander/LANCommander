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

        private List<Expression<Func<T, object>>> IncludeExpressions { get; } = new();
        private User User;

        public Repository(DatabaseContext context, IHttpContextAccessor httpContextAccessor, ILogger<Repository<T>> logger)
        {
            Context = context;
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

                foreach (var includeExpression in IncludeExpressions)
                {
                    queryable = queryable.Include(includeExpression);
                }

                op.Complete();

                return queryable;
            }
        }

        public Repository<T> Include(Expression<Func<T, object>> includeExpression)
        {
            IncludeExpressions.Add(includeExpression);

            return this;
        }

        public async Task <ICollection<T>> Get(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                return await Query(predicate).ToListAsync();
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> First(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                return await Query(predicate).FirstAsync();
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> First<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> Find(Guid id)
        {
            try {
                await Context.Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                {
                    var entity = await Query(x => x.Id == id).FirstAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                {
                    var entity = await Query(predicate).FirstOrDefaultAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                IncludeExpressions.Clear();
                Context.Semaphore.Release();
            }
        }

        public async Task<T> Add(T entity)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

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
                Context.Semaphore.Release();
            }
        }

        public async Task<T> Update(T entity)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

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
                Context.Semaphore.Release();
            }
        }

        public void Delete(T entity)
        {
            try
            {
                Context.Semaphore.Wait();

                using (var op = Logger.BeginOperation("Deleting entity with ID {EntityId}", entity.Id))
                {
                    Context.Remove(entity);

                    op.Complete();
                }
            }
            finally
            {
                Context.Semaphore.Release();
            }
        }

        public async Task SaveChanges()
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                using (var op = Logger.BeginOperation("Saving changes!"))
                {
                    await Context.SaveChangesAsync();

                    op.Complete();
                }
            }
            finally
            {
                Context.Semaphore.Release();
            }
        }

        private async Task<User> GetUser(string username)
        {
            try
            {
                await Context.Semaphore.WaitAsync();

                return await UserDbSet.FirstOrDefaultAsync(u => u.UserName == username);
            }
            finally
            {
                Context.Semaphore.Release();
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
                Context.Semaphore.Release();
                Logger?.LogDebug("Disposed context {ContextId}", Context.ContextId);
            }
            catch {
                Logger?.LogDebug("Could not dispose context {ContextId}", Context.ContextId);
            }
        }
    }
}
