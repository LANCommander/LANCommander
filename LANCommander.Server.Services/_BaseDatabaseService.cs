﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LANCommander.Server.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public abstract class BaseDatabaseService<T>(
        ILogger logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> dbContextFactory) : BaseService(logger), IBaseDatabaseService<T> where T : class, IBaseModel
    {
        protected readonly List<Func<IQueryable<T>, IQueryable<T>>> _modifiers = new();

        public IBaseDatabaseService<T> AsNoTracking()
        {
            return Query((queryable) =>
            {
                return queryable.AsNoTracking();
            });
        }

        public IBaseDatabaseService<T> AsSplitQuery()
        {
            return Query((queryable) =>
            {
                return queryable.AsSplitQuery();
            });
        }

        public IBaseDatabaseService<T> Query(Func<IQueryable<T>, IQueryable<T>> modifier)
        {
            _modifiers.Add(modifier);

            return this;
        }

        public IBaseDatabaseService<T> Include(params string[] includes)
        {
            return Include(includes);
        }

        public IBaseDatabaseService<T> Include(IEnumerable<string> includes)
        {
            return Query((queryable) =>
            {
                foreach (var include in includes)
                {
                    queryable = queryable.Include(include);
                }

                return queryable;
            });
        }

        public IBaseDatabaseService<T> Include(params Expression<Func<T, object>>[] expressions)
        {
            return Query((queryable) =>
            {
                foreach (var expression in expressions)
                {
                    queryable = queryable.Include(expression);
                }

                return queryable;
            });
        }

        public IBaseDatabaseService<T> SortBy(Expression<Func<T, object>> expression, SortDirection direction = SortDirection.Ascending)
        {
            switch (direction)
            {
                case SortDirection.Descending:
                    return Query((queryable) =>
                    {
                        return queryable.OrderByDescending(expression);
                    });
                case SortDirection.Ascending:
                default:
                    return Query((queryable) =>
                    {
                        return queryable.OrderBy(expression);
                    });
            }
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
            return await FirstOrDefaultAsync(x => x.Id == id);
        }

        public virtual async Task<U> GetAsync<U>(Guid id)
        {
            return await FirstOrDefaultAsync<U>(x => x.Id == id);
        }

        public virtual async Task<ICollection<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();

                var queryable = context.Set<T>().AsQueryable();
                
                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);
                
                return await queryable.Where(predicate).ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<ICollection<U>> GetAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                var queryable = context.Set<T>().AsQueryable();
                
                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);
                
                return await queryable.Where(predicate).ProjectTo<U>(mapper.ConfigurationProvider).ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                var queryable = context.Set<T>().AsQueryable();
                
                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);
                
                return await queryable.FirstAsync(predicate);
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<U> FirstAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                var queryable = context.Set<T>().AsQueryable();
                
                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.Where(predicate).ProjectTo<U>(mapper.ConfigurationProvider).FirstAsync();
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                var queryable = context.Set<T>().AsQueryable();
                
                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                return await queryable.FirstOrDefaultAsync(predicate);
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<U> FirstOrDefaultAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();

                var queryable = context.Set<T>().AsQueryable();

                foreach (var modifier in _modifiers)
                    queryable = modifier.Invoke(queryable);

                var entity = await queryable.Where(predicate).FirstOrDefaultAsync();

                return mapper.Map<U>(entity);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task<bool> ExistsAsync(Guid id)
        {
            return (await FirstOrDefaultAsync(x => x.Id == id)) != null;
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return (await GetAsync(predicate)).Any();
        }

        public abstract Task<T> AddAsync(T entity);
        
        protected async Task<T> AddAsync(T addedEntity, Action<UpdateEntityContext<T>> additionalMapping = null)
        {
            try
            {
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                var currentUser = await GetCurrentUserAsync(context);
                
                var newEntity = Activator.CreateInstance<T>();
                
                context.Entry(newEntity).CurrentValues.SetValues(addedEntity);
                
                newEntity.CreatedOn = DateTime.UtcNow;
                newEntity.CreatedBy = currentUser;
                //newEntity.CreatedById = currentUser?.Id;

                if (additionalMapping != null)
                {
                    var updateContext = new UpdateEntityContext<T>(context, newEntity, addedEntity);

                    additionalMapping?.Invoke(updateContext);
                }

                newEntity = (await context.AddAsync(newEntity)).Entity;
                
                await context.SaveChangesAsync();

                return newEntity;
            }
            finally
            {
                Reset();
            }
        }

        /// <summary>
        /// Adds an entity to the database if it does exist as dictated by the predicate
        /// </summary>
        /// <param name="predicate">Qualifier expressoin</param>
        /// <param name="entity">Entity to add</param>
        /// <returns>Newly created or existing entity</returns>
        public virtual async Task<ExistingEntityResult<T>> AddMissingAsync(Expression<Func<T, bool>> predicate, T entity)
        {
            var existing = await FirstOrDefaultAsync(predicate);

            if (existing == null)
            {
                await cache.ExpireAsync($"{typeof(T).FullName}");

                entity = await AddAsync(entity);

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
        
        public abstract Task<T> UpdateAsync(T entity);

        protected async Task<T> UpdateAsync(T updatedEntity, Action<UpdateEntityContext<T>> additionalMapping = null)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();

            if (updatedEntity.CreatedById != null && updatedEntity.CreatedBy == null)
                updatedEntity.CreatedById = null;
            
            var existingEntity = await context.Set<T>().FirstOrDefaultAsync(e => e.Id == updatedEntity.Id);
            
            context.Entry(existingEntity).CurrentValues.SetValues(updatedEntity);

            if (additionalMapping != null)
            {
                var updateContext = new UpdateEntityContext<T>(context, existingEntity, updatedEntity);
                
                additionalMapping?.Invoke(updateContext);
            }
            
            var currentUser = await GetCurrentUserAsync(context);
            
            existingEntity.UpdatedOn = DateTime.UtcNow;
            existingEntity.UpdatedById = currentUser?.Id;

            await context.SaveChangesAsync();
            
            return updatedEntity;
        }

        public virtual async Task DeleteAsync(T entity)
        {
            try
            {
                await cache.ExpireAsync($"{typeof(T).FullName}");
                
                using var context = await dbContextFactory.CreateDbContextAsync();
                
                context.Set<T>().Remove(entity);
                
                await context.SaveChangesAsync();
            }
            finally
            {
                Reset();
            }
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            try
            {
                await cache.ExpireAsync($"{typeof(T).FullName}");

                using var context = await dbContextFactory.CreateDbContextAsync();

                context.Set<T>().RemoveRange(entities);

                await context.SaveChangesAsync();
            }
            finally
            {
                Reset();
            }
        }

        private async Task<User?> GetCurrentUserAsync(DatabaseContext context)
        {
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext != null && httpContext.User != null && httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated)
            {
                return await GetUserAsync(httpContext.User.Identity?.Name, context);
            }

            return null;
        }
        
        private static async Task<User?> GetUserAsync(string? username, DatabaseContext context) =>
            await context.Users.FirstOrDefaultAsync(u => u.UserName == username);

        protected void Reset()
        {
            _modifiers.Clear();
        }
    }
}
