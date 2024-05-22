using LANCommander.Client.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LANCommander.Client.Data
{
    public class Repository<T> : IDisposable where T : BaseModel
    {
        private DbContext Context;

        public Repository(DatabaseContext context)
        {
            Context = context;
        }

        private DbSet<T> DbSet
        {
            get { return Context.Set<T>(); }
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
            entity.CreatedOn = DateTime.Now;
            entity.UpdatedOn = DateTime.Now;

            await Context.AddAsync(entity);

            return entity;
        }

        public T Update(T entity)
        {
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

        public void Dispose()
        {
            
        }
    }
}
