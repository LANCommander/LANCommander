using LANCommander.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LANCommander.Data
{
    public class Repository<T> : IDisposable where T : BaseModel
    {
        private DbContext Context;
        private HttpContext HttpContext;

        public Repository(DatabaseContext context, HttpContext httpContext)
        {
            Context = context;
            HttpContext = httpContext;
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
            return DbSet.AsQueryable().Where(predicate);
        }

        public async Task<T> Find(Guid id)
        {
            return await DbSet.FindAsync(id);
        }

        public T FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            return Get(predicate).FirstOrDefault();
        }

        public async Task<T> Add(T entity)
        {
            entity.CreatedById = GetCurrentUserId();
            entity.UpdatedById = GetCurrentUserId();
            entity.CreatedOn = DateTime.Now;
            entity.UpdatedOn = DateTime.Now;

            await Context.AddAsync(entity);

            return entity;
        }

        public T Update(T entity)
        {
            entity.UpdatedById = GetCurrentUserId();
            entity.UpdatedOn = DateTime.Now;

            Context.Update(entity);

            return entity;
        }

        public async Task SaveChanges()
        {
            await Context.SaveChangesAsync();
        }

        private User GetUser(string username)
        {
            return UserDbSet.FirstOrDefault(u => u.UserName == username);
        }

        private Guid GetCurrentUserId()
        {
            if (HttpContext != null && HttpContext.User != null && HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
            {
                var user = GetUser(HttpContext.User.Identity.Name);

                if (user == null)
                    return Guid.Empty;
                else
                    return Guid.Parse(user.Id);
            }
            else
                return Guid.Empty;
        }

        public void Dispose()
        {
            
        }
    }
}
