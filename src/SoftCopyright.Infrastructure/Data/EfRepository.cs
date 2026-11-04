using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Infrastructure.Data;

/// <summary>EF Core 通用仓储实现。</summary>
public sealed class EfRepository<T>(SoftCopyrightDbContext db) : IRepository<T> where T : class
{
    public async Task<T?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Set<T>().FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct) =>
        predicate is null
            ? await db.Set<T>().ToListAsync(ct)
            : await db.Set<T>().Where(predicate).ToListAsync(ct);

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct) =>
        predicate is null
            ? await db.Set<T>().CountAsync(ct)
            : await db.Set<T>().CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct) => await db.Set<T>().AddAsync(entity, ct);

    public void Remove(T entity) => db.Set<T>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
