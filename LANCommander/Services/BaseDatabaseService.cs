using LANCommander.Data;
using LANCommander.Data.Models;
using System.Linq.Expressions;

namespace LANCommander.Services
{
    public abstract class BaseDatabaseService<T> where T : BaseModel
    {
        public DatabaseContext Context { get; set; }
        public HttpContext HttpContext { get; set; }

        public BaseDatabaseService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) {
            Context = dbContext;
            HttpContext = httpContextAccessor.HttpContext;
        }

        public virtual ICollection<T> Get()
        {
            return Get(x => true).ToList();
        }

        public virtual async Task<T> Get(Guid id)
        {
            using (var repo = new Repository<T>(Context, HttpContext))
            {
                return await repo.Find(id);
            }
        }

        public virtual IQueryable<T> Get(Expression<Func<T, bool>> predicate)
        {
            using (var repo = new Repository<T>(Context, HttpContext))
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
            using (var repo = new Repository<T>(Context, HttpContext))
            {
                entity = await repo.Add(entity);
                await repo.SaveChanges();

                return entity;
            }
        }

        public virtual async Task<T> Update(T entity)
        {
            using (var repo = new Repository<T>(Context, HttpContext))
            {
                entity = repo.Update(entity);
                await repo.SaveChanges();

                return entity;
            }
        }

        public virtual async Task Delete(T entity)
        {
            using (var repo = new Repository<T>(Context, HttpContext))
            {
                repo.Delete(entity);
                await repo.SaveChanges();
            }
        }
    }
}
