using LANCommander.SDK.Extensions;
using LANCommander.SDK;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LANCommander.Server.Data
{
    public class Repository<T> : IDisposable where T : class, IBaseModel
    {
        public readonly DatabaseContext Context;
        private readonly IHttpContextAccessor HttpContextAccessor;
        private readonly ILogger Logger;

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

        public IQueryable<T> Get(Expression<Func<T, bool>> predicate)
        {
            using (var op = Logger.BeginOperation("Querying database"))
            {
                var queryable = DbSet.AsQueryable().Where(predicate);

                op.Complete();

                return queryable;
            }
        }

        public async Task<T> Find(Guid id)
        {
            using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
            {
                var entity = await DbSet.FindAsync(id);

                op.Complete();

                return entity;
            }
        }

        public async Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
            {
                var entity = await Get(predicate).FirstOrDefaultAsync();

                op.Complete();

                return entity;
            }
        }

        public async Task<T> Add(T entity)
        {
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

        public async Task<T> Update(T entity)
        {
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

        public void Delete(T entity)
        {
            using (var op = Logger.BeginOperation("Deleting entity with ID {EntityId}", entity.Id))
            {
                Context.Remove(entity);

                op.Complete();
            }
        }

        public async Task SaveChanges()
        {
            using (var op = Logger.BeginOperation("Saving changes!"))
            {
                await Context.SaveChangesAsync();

                op.Complete();
            }
        }

        private async Task<User> GetUser(string username)
        {
            return await UserDbSet.FirstOrDefaultAsync(u => u.UserName == username);
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
                Context.Dispose();
                Logger?.LogDebug("Disposed context {ContextId}", Context.ContextId);
            }
            catch {
                Logger?.LogDebug("Could not dispose context {ContextId}", Context.ContextId);
            }
        }
    }
}
