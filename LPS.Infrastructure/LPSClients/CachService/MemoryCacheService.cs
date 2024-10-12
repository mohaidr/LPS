// MemoryCacheService.cs
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Caching
{
    public class MemoryCacheService<T>(IMemoryCache memoryCache,
        TimeSpan? defaultCacheDuration = null) : ICacheService<T>
    {
        private readonly IMemoryCache _memoryCache = memoryCache;
        private readonly TimeSpan _defaultCacheDuration = defaultCacheDuration ?? TimeSpan.FromSeconds(30);

        public Task<T> GetItemAsync(string key)
        {
            _memoryCache.TryGetValue(key, out T item);
            return Task.FromResult(item);
        }

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task SetItemAsync(string key, T item, TimeSpan? duration = null)
        {
            bool semaphoreAcquired = false;
            try
            {
                var cacheDuration = duration ?? _defaultCacheDuration;
                MemoryCacheEntryOptions cacheEntryOptions;

                if (cacheDuration == TimeSpan.MaxValue)
                {
                    cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSize(1)
                        .SetAbsoluteExpiration(DateTimeOffset.MaxValue);
                }
                else
                {
                    cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(cacheDuration)
                        .SetSize(1);
                }

                // Ensure thread-safety using a semaphore
                await _semaphore.WaitAsync();
                semaphoreAcquired = true;
                _memoryCache.Set(key, item, cacheEntryOptions);
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _semaphore.Release();
                }
            }
        }

        public bool TryGetItem(string key, out T item)
        {
            return _memoryCache.TryGetValue(key, out item);
        }

        public async Task RemoveItemAsync(string key)
        {
            await Task.Run(() => _memoryCache.Remove(key));
        }
    }
}
