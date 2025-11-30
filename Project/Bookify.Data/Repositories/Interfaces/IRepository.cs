using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Repositories.Interfaces
{
    public interface IRepository <T> where T : class
    {
        public Task<IEnumerable<T>> GetAllAsync();
        public Task<T?> GetByIdAsync(int Id);
        public Task<T?> GetByIdAsync(string Id);
        public Task<T> AddAsync(T Entity);
        public Task AddRangeAsync(IEnumerable<T> entities);
        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> Predicate);
        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> Predicate);
        public void Remove(T Entity);
        public void RemoveRange(IEnumerable<T> entities);
        public void Update(T Entity);
        public Task<int> CountAsync(Expression<Func<T,bool>>? Predicate= null);
        public Task <bool> AnyAsync(Expression<Func<T,bool>>Predicate);

    }
}
