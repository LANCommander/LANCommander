using System.Collections;
using LANCommander.SDK.Extensions;
using LANCommander.SDK;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Linq;
using System.Threading;
using AutoMapper.QueryableExtensions;
using AutoMapper;
using LANCommander.Server.Data.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharpCompress.Common;

namespace LANCommander.Server.Data
{
    public class Repository<T> : IDisposable where T : class, IBaseModel
    {
        public readonly DatabaseContext Context;
        private readonly IMapper Mapper;
        private readonly IHttpContextAccessor HttpContextAccessor;
        private readonly ILogger Logger;

        private List<Func<IQueryable<T>, IQueryable<T>>> Modifiers = new List<Func<IQueryable<T>, IQueryable<T>>>();

        private User User;

        public Repository(
            IDbContextFactory<DatabaseContext> contextFactory,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            ILogger logger)
        {
            Context = contextFactory.CreateDbContext();
            Mapper = mapper;
            HttpContextAccessor = httpContextAccessor;
            Logger = logger;

            Logger?.LogDebug("Opened up context {ContextId}", Context.ContextId);
        }

        public Repository(
            IDbContextFactory<DatabaseContext> contextFactory,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor)
        {
            Context = contextFactory.CreateDbContext();
            Mapper = mapper;
            HttpContextAccessor = httpContextAccessor;
        }

        private DbSet<T> DbSet
        {
            get { return Context.Set<T>(); }
        }

        private DbSet<User> UserDbSet
        {
            get { return Context.Set<User>(); }
        }

        public Repository<T> Query(Func<IQueryable<T>, IQueryable<T>> modifier)
        {
            Modifiers.Add(modifier);

            return this;
        }

        private IQueryable<T> Query(Expression<Func<T, bool>> predicate)
        {
            var queryable = DbSet.AsQueryable().Where(predicate);

            foreach (var modifier in Modifiers)
            {
                queryable = modifier.Invoke(queryable);
            }

            if (Modifiers.Any(m => m.Method.Name.StartsWith("<Include>")))
                queryable = queryable.AsSplitQuery();

            return queryable;
        }

        public IQueryable<T> Query()
        {
            return DbSet.AsQueryable();
        }

        public Repository<T> AsNoTracking()
        {
            return Query((queryable) =>
            {
                return queryable.AsNoTracking();
            });
        }

        public Repository<T> Include(params Expression<Func<T, object>>[] expressions)
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

        public Repository<T> SortBy(Expression<Func<T, object>> expression, SortDirection direction = SortDirection.Ascending)
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

        /// <summary>
        /// With the current query, get a paginated list of results. Optimizes the query by only getting the amount of records specified by page size.
        /// </summary>
        /// <param name="expression">The query to filter results by</param>
        /// <param name="pageNumber">The current page number (indexed by 1)</param>
        /// <param name="pageSize">The number of results to get per page</param>
        /// <returns></returns>
        public async Task<PaginatedResults<T>> PaginateAsync(Expression<Func<T, bool>> expression, int pageNumber, int pageSize)
        {
            try
            {
                var results = new PaginatedResults<T>();

                results.Count = await Query(expression).CountAsync();
                results.Results = await Query(expression).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                return results;
            }
            finally
            {
                Modifiers.Clear();
            }
        }

        public async Task<ICollection<T>> GetAsync()
        {
            try
            {
                return await Query().ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task <ICollection<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                return await Query(predicate).ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<ICollection<U>> GetAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).ToListAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                return await Query(predicate).FirstAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<U> FirstAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).FirstAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstAsync<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                return await Query(predicate).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<U> FirstAsync<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            try
            {
                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).OrderByDescending(orderKeySelector).FirstAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FindAsync(Guid id)
        {
            try {
                //using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                //{
                    var entity = await Query(x => x.Id == id).FirstAsync();

                ///    op.Complete();

                    return entity;
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task<U> FindAsync<U>(Guid id)
        {
            try
            {
                //using (var op = Logger.BeginOperation("Finding entity with ID {EntityId}", id))
                //{
                    var entity = await Query(x => x.Id == id).ProjectTo<U>(Mapper.ConfigurationProvider).FirstAsync();

                //    op.Complete();

                    return entity;
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                //using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                //{
                    var entity = await Query(predicate).FirstOrDefaultAsync();

                //    op.Complete();

                    return entity;
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task<U> FirstOrDefaultAsync<U>(Expression<Func<T, bool>> predicate)
        {
            try
            {
                //using (var op = Logger.BeginOperation("Getting first or default of type {EntityType}", typeof(T).Name))
                //{
                    var entity = await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).FirstOrDefaultAsync();

                //    op.Complete();

                    return entity;
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> FirstOrDefaultAsync<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector)
        {
            try
            {
                return await Query(predicate).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<U> FirstOrDefaultAsync<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector)
        {
            try
            {
                return await Query(predicate).ProjectTo<U>(Mapper.ConfigurationProvider).OrderByDescending(orderKeySelector).FirstOrDefaultAsync();
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> AddAsync(T entity)
        {
            try
            {
                var currentUser = await GetCurrentUserId();

                //using (var op = Logger.BeginOperation("Adding entity of type {EntityType}", typeof(T).Name))
                //{
                    entity.CreatedById = currentUser;
                    entity.UpdatedById = currentUser;
                    entity.CreatedOn = DateTime.UtcNow;
                    entity.UpdatedOn = DateTime.UtcNow;

                    await Context.AddAsync(entity);

                    //op.Complete();

                    return entity;
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task<T> UpdateAsync(T entity)
        {
            try
            {
                var currentUserId = await GetCurrentUserId();

                entity.UpdatedById = currentUserId;
                entity.UpdatedOn = DateTime.UtcNow;

                SafeAttach(entity);
                
                return entity;
            }
            catch (Exception ex)
            {
                return entity;
            }
            finally
            {
                Reset();
            }
        }

        public void Delete(T entity)
        {
            try
            {
                //using (var op = Logger.BeginOperation("Deleting entity with ID {EntityId}", entity.Id))
                //{
                    Context.Remove(entity);

                //    op.Complete();
                //}
            }
            finally
            {
                Reset();
            }
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                //using (var op = Logger.BeginOperation("Saving changes!"))
                //{
                    await Context.SaveChangesAsync();

                //    op.Complete();
                //}
            }
            finally
            {
                Reset();
            }
        }

        protected void SafeAttach<TEntity>(TEntity entity)
            where TEntity : IBaseModel
        {
            Context.ChangeTracker.TrackGraph(entity, node =>
            {
                var id = entity.Id;
                var entityType = node.Entry.Metadata;
                
                var existingEntry = node.Entry.Context.ChangeTracker.Entries()
                    .Where(e => e.Entity is IBaseModel)
                    .FirstOrDefault(e => (e.Entity as IBaseModel).Id == id);

                if (existingEntry == null)
                {
                    if (id == Guid.Empty)
                        node.Entry.State = EntityState.Added;
                    
                    SafeAttachChildren(node.Entry.Entity as IBaseModel);
                }
                
                //else
                //    Logger.LogDebug($"Discarding duplicate {entityType.DisplayName()} entity with ID {id}");
            });
        }

        private void SafeAttachChildren<TEntity>(TEntity entity)
            where TEntity : class, IBaseModel
        {
            // This is possibly the worst way of handling this. This should be refactored in the future.
            // Really all this does is make sure that any navigation collections are set correctly and
            // populated from existing data without introducing duplicates in the change tracker.
            
            var entry = Context.Entry(entity);
            entry.State = EntityState.Detached;
            
            // Get the actual state of the entity from the database
            var existing = Context.Find(entity.GetType(), entity.Id);
            
            foreach (var property in Context.Entry(entity).CurrentValues.Properties)
            {
                if (!property.IsPrimaryKey())
                {
                    Context.Entry(existing).Property(property.Name).CurrentValue =
                        entry.Property(property.Name).CurrentValue;
                    Context.Entry(existing).Property(property.Name).IsModified =
                        true;
                }
            }

            foreach (var navigation in entry.Navigations)
            {
                if (navigation.CurrentValue is not null)
                {
                    if (navigation.CurrentValue is IBaseModel navigationEntity)
                    {
                        // Not needed? Seems like _:1 relationships work fine.
                        
                        /*if (navigationEntity.Id != Guid.Empty)
                            SafeAttach(navigationEntity);
                        else if (navigationEntity.Id == entity.Id)
                            continue; // Probably already attached because it was passed into this method*/
                    }
                    else if (navigation is CollectionEntry && navigation.CurrentValue != null)
                    {
                        var collectionType = navigation.CurrentValue.GetType();

                        if (typeof(IEnumerable<IBaseModel>).IsAssignableFrom(collectionType))
                        {
                            if (existing != null)
                            {
                                // Load the navigation collection by property name
                                Context.Entry(existing).Collection(navigation.Metadata.Name).Load();
                                
                                var existingCollection = Context.Entry(existing)
                                    .Collection(navigation.Metadata.Name)
                                    .CurrentValue;

                                // The real jank. Make the most generic List possible without knowing what type is used
                                // for the collection's generic type.
                                var genericListType = typeof(List<>).MakeGenericType(collectionType.GetGenericArguments()[0]);
                                
                                IList updatedList = Activator.CreateInstance(genericListType) as IList;

                                // We have no access to LINQ sexiness here. Manually iterate through the collection.
                                foreach (var item in existingCollection)
                                {
                                    var existsInNew = false;

                                    foreach (var newItem in navigation.CurrentValue as IEnumerable)
                                    {
                                        if ((item as IBaseModel).Id == (newItem as IBaseModel).Id && !existsInNew)
                                        {
                                            foreach (var property in Context.Entry(newItem).CurrentValues.Properties)
                                            {
                                                if (!property.IsPrimaryKey())
                                                {
                                                    Context.Entry(item).Property(property.Name).CurrentValue =
                                                        Context.Entry(newItem).Property(property.Name).CurrentValue;
                                                    Context.Entry(item).Property(property.Name).IsModified =
                                                        true;
                                                }
                                            }
                                            
                                            Context.Entry(newItem).State = EntityState.Detached;
                                            existsInNew = true;
                                            
                                            updatedList.Add(item);
                                        }
                                    }
                                }
                                
                                // May not have iterated over any existing items in the collection, rerun through and
                                // check against updated list
                                foreach (var newItem in navigation.CurrentValue as IEnumerable)
                                {
                                    var alreadyAdded = false;

                                    foreach (var updatedListItem in updatedList)
                                    {
                                        if ((newItem as IBaseModel).Id == (updatedListItem as IBaseModel).Id &&
                                            !alreadyAdded)
                                        {
                                            alreadyAdded = true;
                                        }
                                    }
                                    
                                    if (!alreadyAdded)
                                        updatedList.Add(newItem);
                                }

                                Context.Entry(existing).Collection(navigation.Metadata.Name).CurrentValue = updatedList;
                                Context.Entry(existing).Collection(navigation.Metadata.Name).IsModified = true;
                            }
                            
                            // Not sure if this is needed, introduces recursion pretty badly
                            
                            /*foreach (var item in navigation.CurrentValue as List<IBaseModel>)
                            {
                                if (item.Id != Guid.Empty && item.Id != entity.Id)
                                    SafeAttach(item);
                            }*/
                        }
                    }
                }
            }
        }

        private async Task<User> GetUser(string username)
        {
            try
            {
                return await UserDbSet.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == username);
            }
            finally
            {
                Reset();
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

        private void Reset()
        {
            Modifiers.Clear();
        }

        public void Dispose()
        {
            try
            {
                Context.Dispose();
            }
            catch { }
        }
    }
}
