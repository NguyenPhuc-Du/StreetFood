using System.Collections.Concurrent;
using StreetFood.API.Models.Admin;

namespace StreetFood.API.Services;

public sealed class PoiIngressQueueService
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    // High-throughput default: keep per-POI ordering but no artificial sleep.
    private int _enabled = 1;
    private int _minDelayMs = 0;
    private int _maxDelayMs = 0;
    private long _contentionCount;

    public PoiIngressQueueSettingsDto GetSettings()
    {
        return new PoiIngressQueueSettingsDto(
            Volatile.Read(ref _enabled) == 1,
            Volatile.Read(ref _minDelayMs),
            Volatile.Read(ref _maxDelayMs),
            Interlocked.Read(ref _contentionCount));
    }

    public PoiIngressQueueSettingsDto Update(bool? enabled, int? minDelayMs, int? maxDelayMs)
    {
        if (enabled.HasValue)
            Interlocked.Exchange(ref _enabled, enabled.Value ? 1 : 0);

        var curMin = Volatile.Read(ref _minDelayMs);
        var curMax = Volatile.Read(ref _maxDelayMs);
        var nextMin = minDelayMs.HasValue ? Math.Clamp(minDelayMs.Value, 0, 500) : curMin;
        var nextMax = maxDelayMs.HasValue ? Math.Clamp(maxDelayMs.Value, 0, 1000) : curMax;
        if (nextMax < nextMin)
            nextMax = nextMin;

        Interlocked.Exchange(ref _minDelayMs, nextMin);
        Interlocked.Exchange(ref _maxDelayMs, nextMax);

        return GetSettings();
    }

    public async Task<PoiIngressQueueLease> EnterAsync(int poiId, CancellationToken cancellationToken = default)
    {
        if (poiId <= 0 || Volatile.Read(ref _enabled) == 0)
            return PoiIngressQueueLease.Noop;

        var sem = _locks.GetOrAdd(poiId, _ => new SemaphoreSlim(1, 1));
        var hadContention = sem.CurrentCount == 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await sem.WaitAsync(cancellationToken);
        sw.Stop();

        var waitedMs = (int)sw.ElapsedMilliseconds;
        var jitterMs = 0;
        if (hadContention || waitedMs > 0)
        {
            Interlocked.Increment(ref _contentionCount);
            var min = Volatile.Read(ref _minDelayMs);
            var max = Volatile.Read(ref _maxDelayMs);
            jitterMs = max > min ? Random.Shared.Next(min, max + 1) : min;
            if (jitterMs > 0)
                await Task.Delay(jitterMs, cancellationToken);
        }

        return new PoiIngressQueueLease(sem, waitedMs, jitterMs, hadContention || waitedMs > 0);
    }
}

public sealed class PoiIngressQueueLease : IDisposable
{
    private readonly SemaphoreSlim? _semaphore;

    public static PoiIngressQueueLease Noop { get; } = new(null, 0, 0, false);

    public PoiIngressQueueLease(SemaphoreSlim? semaphore, int waitedMs, int jitterMs, bool wasQueued)
    {
        _semaphore = semaphore;
        WaitedMs = waitedMs;
        JitterMs = jitterMs;
        WasQueued = wasQueued;
    }

    public int WaitedMs { get; }
    public int JitterMs { get; }
    public int TotalDelayMs => WaitedMs + JitterMs;
    public bool WasQueued { get; }

    public void Dispose()
    {
        _semaphore?.Release();
    }
}
