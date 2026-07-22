using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly NileChainDbContext Context;
    public Repository(NileChainDbContext context) => Context = context;

    public async Task<T?> GetByIdAsync(Guid id) => await Context.Set<T>().FindAsync(id);
    public async Task<IReadOnlyList<T>> GetAllAsync() => await Context.Set<T>().ToListAsync();
    public async Task AddAsync(T entity) => await Context.Set<T>().AddAsync(entity);
    public void Update(T entity) => Context.Set<T>().Update(entity);
    public void Remove(T entity) => Context.Set<T>().Remove(entity);
}
