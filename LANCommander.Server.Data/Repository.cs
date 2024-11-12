using LANCommander.SDK.Extensions;
using LANCommander.SDK;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Threading;
using AutoMapper.QueryableExtensions;
using AutoMapper;

namespace LANCommander.Server.Data
{
    public class Repository<T> : IDisposable where T : class, IBaseModel
    {
        public readonly DatabaseContext Context;
        private readonly IMapper Mapper;
        private readonly IHttpContextAccessor HttpContextAccessor;
        private readonly ILogger Logger;

        private List<Expression<Func<T, object>>> IncludeExpressions { get; } = new();
        private User User;
        private bool Tracking = true;

        public Repository(
            DatabaseContext context,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ILogger<Repository<T>> logger)
        {
            Context = context;
            Mapper = mapper;
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

                if (!Tracking)
                    queryable = queryable.AsNoTracking();

                return queryable;
            }
        }

        public Repository<T> AsNoTracking()
        {
            Tracking = false;

            return this;
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
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).ToListAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<ICollection<U>> Get<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).ToListAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> First(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).FirstAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<U> First<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).FirstAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> First<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<U> First<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> Find(Guid id)
        {
            try {
                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                {
                    var entity = await Query(x => x.Id == id).FirstAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<U> Find<U>(Guid id)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                {
                    var entity = await Query(x => x.Id == id).ProjectTo<U>(Mapper.ConfigurationProvider).FirstAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                {
                    var entity = await Query(predicate).FirstOrDefaultAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<U> FirstOrDefault<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                {
                    var entity = await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).FirstOrDefaultAsync();

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<U> FirstOrDefault<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                Tracking = true;
                IncludeExpressions.Clear();
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> Add(T entity)
        {
            try
            {
                var currentUser = await GetCurrentUserId();

                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Adding entity of type {EntityType}", typeof(T).Name))
                {
                    entity.CreatedById = currentUser;
                    entity.UpdatedById = currentUser;
                    entity.CreatedOn = DateTime.UtcNow;
                    entity.UpdatedOn = DateTime.UtcNow;

                    await Context.AddAsync(entity);

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                Context.ContextMutex.Release();
            }
        }

        public async Task<T> Update(T entity)
        {
            try
            {
                var currentUserId = await GetCurrentUserId();
                var existing = await Find(entity.Id);

                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Updating entity with ID {EntityId}", entity.Id))
                {
                    Context.Entry(existing).CurrentValues.SetValues(entity);

                    entity.UpdatedById = currentUserId;
                    entity.UpdatedOn = DateTime.UtcNow;

                    Context.Update(entity);

                    op.Complete();

                    return entity;
                }
            }
            finally
            {
                Tracking = true;
                Context.ContextMutex.Release();
            }
        }

        public void Delete(T entity)
        {
            try
            {
                Context.ContextMutex.Wait();

                using (var op = Logger.BeginOperation("Deleting entity with ID {EntityId}", entity.Id))
                {
                    Context.Remove(entity);

                    op.Complete();
                }
            }
            finally
            {
                Tracking = true;
                Context.ContextMutex.Release();
            }
        }

        public async Task SaveChanges()
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                using (var op = Logger.BeginOperation("Saving changes!"))
                {
                    await Context.SaveChangesAsync();

                    op.Complete();
                }
            }
            finally
            {
                Tracking = true;
                Context.ContextMutex.Release();
            }
        }

        private async Task<User> GetUser(string username)
        {
            try
            {
                await Context.ContextMutex.WaitAsync();

                return await UserDbSet.FirstOrDefaultAsync(u => u.UserName == username);
            }
            finally
            {
                Tracking = true;
                Context.ContextMutex.Release();
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
                Context.ContextMutex.Release();
                Logger?.LogDebug("Disposed context {ContextId}", Context.ContextId);
            }
            catch {
                Logger?.LogDebug("Could not dispose context {ContextId}", Context.ContextId);
            }
        }
    }
}
