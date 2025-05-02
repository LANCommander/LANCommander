using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using System.Linq.Expressions;

namespace LANCommander.Server.Services.Abstractions
{
    public interface IBaseDatabaseService<T> where T : class, IBaseModel
    {
        IBaseDatabaseService<T> Query(Func<IQueryable<T>, IQueryable<T>> modifier);
        IBaseDatabaseService<T> Include(params Expression<Func<T, object>>[] expressions);
        IBaseDatabaseService<T> SortBy(Expression<Func<T, object>> expression, SortDirection direction = SortDirection.Ascending);
        IBaseDatabaseService<T> AsNoTracking();
        IBaseDatabaseService<T> AsSplitQuery();

        Task<ICollection<T>> GetAsync();
        Task<ICollection<U>> GetAsync<U>();

        Task<T> GetAsync(Guid id);
        Task<U> GetAsync<U>(Guid id);

        Task<ICollection<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<ICollection<U>> GetAsync<U>(Expression<Func<T, bool>> predicate);

        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<U> FirstOrDefaultAsync<U>(Expression<Func<T, bool>> predicate);

        Task<bool> ExistsAsync(Guid id);

        Task<T> AddAsync(T entity);

        Task<ExistingEntityResult<T>> AddMissingAsync(Expression<Func<T, bool>> predicate, T entity);

        Task<T> UpdateAsync(T entity);

        Task DeleteAsync(T entity);
        // TODO: Add IBaseDatabaseService<>.DeleteRangeAsync
        //Task DeleteRangeAsync(IEnumerable<T> entities);
    }
}
