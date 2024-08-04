using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LANCommander.Server.Data
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
            entity.CreatedBy = GetCurrentUser();
            entity.UpdatedBy = GetCurrentUser();
            entity.CreatedOn = DateTime.Now;
            entity.UpdatedOn = DateTime.Now;

            await Context.AddAsync(entity);

            return entity;
        }

        public T Update(T entity)
        {
            entity.UpdatedBy = GetCurrentUser();
            entity.UpdatedOn = DateTime.Now;

            Context.Update(entity);

            return entity;
        }

        public void Delete(T entity)
        {
            Context.Remove(entity);
        }

        public async Task SaveChanges()
        {
            await Context.SaveChangesAsync();
        }

        private User GetUser(string username)
        {
            return UserDbSet.FirstOrDefault(u => u.UserName == username);
        }

        private User GetCurrentUser()
        {
            if (HttpContext != null && HttpContext.User != null && HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
            {
                var user = GetUser(HttpContext.User.Identity.Name);

                if (user == null)
                    return null;
                else
                    return user;
            }
            else
                return null;
        }

        public void Dispose()
        {
            
        }
    }
}
