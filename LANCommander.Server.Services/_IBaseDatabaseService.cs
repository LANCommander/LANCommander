using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LANCommander.Server.Services
{
    public interface IBaseDatabaseService<T> where T : class, IBaseModel
    {
        Repository<T> Repository { get; set; }

        IBaseDatabaseService<T> Include(Expression<Func<T, object>> includeExpression);

        Task<ICollection<T>> Get();
        Task<ICollection<U>> Get<U>();

        Task<T> Get(Guid id);
        Task<U> Get<U>(Guid id);

        Task<ICollection<T>> Get(Expression<Func<T, bool>> predicate);
        Task<ICollection<U>> Get<U>(Expression<Func<T, bool>> predicate);

        Task<T> FirstOrDefault(Expression<Func<T, bool>> predicate);
        Task<U> FirstOrDefault<U>(Expression<Func<T, bool>> predicate);

        Task<T> FirstOrDefault<TKey>(Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderKeySelector);
        Task<U> FirstOrDefault<U, TKey>(Expression<Func<T, bool>> predicate, Expression<Func<U, TKey>> orderKeySelector);

        Task<bool> Exists(Guid id);

        Task<T> Add(T entity);

        Task<ExistingEntityResult<T>> AddMissing(Expression<Func<T, bool>> predicate, T entity);

        Task<T> Update(T entity);

        Task Delete(T entity);
    }
}
