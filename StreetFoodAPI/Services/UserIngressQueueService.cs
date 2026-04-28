using System.Collections.Concurrent;

namespace StreetFood.API.Services;

public sealed class UserIngressQueueService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<UserIngressLease> EnterAsync(string? key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return UserIngressLease.Noop;

        var normalized = key.Trim();
        if (normalized.Length == 0)
            return UserIngressLease.Noop;

        var sem = _locks.GetOrAdd(normalized, static _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken);
        return new UserIngressLease(sem);
    }
}

public sealed class UserIngressLease : IDisposable
{
    private readonly SemaphoreSlim? _semaphore;
    public static UserIngressLease Noop { get; } = new(null);

    public UserIngressLease(SemaphoreSlim? semaphore)
    {
        _semaphore = semaphore;
    }

    public void Dispose()
    {
        _semaphore?.Release();
    }
}
