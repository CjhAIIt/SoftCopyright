using System.Linq.Expressions;
using System.Reflection;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Tests.Support;

/// <summary>测试用内存仓储。</summary>
public sealed class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly List<T> _items;

    public InMemoryRepository(IEnumerable<T>? seed = null)
    {
        _items = seed?.ToList() ?? [];
    }

    public IReadOnlyList<T> Items => _items;

    public Task<T?> FindAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.FirstOrDefault(item => GetId(item) == id));

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct)
    {
        if (predicate is null)
        {
            return Task.FromResult<IReadOnlyList<T>>(_items.ToList());
        }

        var compiled = predicate.Compile();
        return Task.FromResult<IReadOnlyList<T>>(_items.Where(compiled).ToList());
    }

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct)
    {
        if (predicate is null)
        {
            return Task.FromResult(_items.Count);
        }

        var compiled = predicate.Compile();
        return Task.FromResult(_items.Count(compiled));
    }

    public Task AddAsync(T entity, CancellationToken ct)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(T entity) => _items.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);

    private static Guid GetId(T item)
    {
        var property = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(item) is Guid id ? id : Guid.Empty;
    }
}
