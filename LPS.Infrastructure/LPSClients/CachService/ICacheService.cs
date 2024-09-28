// ICacheService.cs
using System;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Caching
{
    public interface ICacheService<T>
    {
        Task<T> GetItemAsync(string key);
        Task SetItemAsync(string key, T item, TimeSpan? duration = null);
        bool TryGetItem(string key, out T item);
        Task RemoveItemAsync(string key);
    }
}
