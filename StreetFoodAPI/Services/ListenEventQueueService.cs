using System.Collections.Concurrent;
using System.Threading.Channels;
using Dapper;
using Npgsql;

namespace StreetFood.API.Services;

public sealed record ListenEventQueueItem(int PoiId, int DurationSeconds, string? DeviceId);

public sealed class ListenEventQueueService
{
    private readonly Channel<ListenEventQueueItem> _channel;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new();
    private readonly TimeSpan _duplicateWindow = TimeSpan.FromSeconds(15);

    public ListenEventQueueService()
    {
        _channel = Channel.CreateBounded<ListenEventQueueItem>(new BoundedChannelOptions(100_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<ListenEventQueueItem> Reader => _channel.Reader;

    public bool IsDuplicate(int poiId, int durationSeconds, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        var now = DateTimeOffset.UtcNow;
        var dev = deviceId.Trim();
        for (var d = durationSeconds - 2; d <= durationSeconds + 2; d++)
        {
            var key = $"{poiId}|{dev}|{d}";
            if (_recent.TryGetValue(key, out var at) && now - at < _duplicateWindow)
                return true;
        }

        return false;
    }

    public async Task EnqueueAsync(ListenEventQueueItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.DeviceId))
        {
            var key = $"{item.PoiId}|{item.DeviceId.Trim()}|{item.DurationSeconds}";
            _recent[key] = DateTimeOffset.UtcNow;
        }

        await _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public void CleanupRecent()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _recent)
        {
            if (now - kv.Value > _duplicateWindow)
                _recent.TryRemove(kv.Key, out _);
        }
    }
}

public sealed class ListenEventQueueWorker : BackgroundService
{
    private readonly ListenEventQueueService _queue;
    private readonly ILogger<ListenEventQueueWorker> _logger;
    private readonly string _connStr;
    private readonly List<ListenEventQueueItem> _buffer = new(1024);

    public ListenEventQueueWorker(
        ListenEventQueueService queue,
        IConfiguration config,
        ILogger<ListenEventQueueWorker> logger)
    {
        _queue = queue;
        _logger = logger;
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushEvery = TimeSpan.FromMilliseconds(200);
        var maxBatch = 500;
        var nextFlushAt = DateTime.UtcNow + flushEvery;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (_queue.Reader.TryRead(out var item))
                {
                    _buffer.Add(item);
                    if (_buffer.Count >= maxBatch)
                        break;
                }

                if (_buffer.Count >= maxBatch || (_buffer.Count > 0 && DateTime.UtcNow >= nextFlushAt))
                {
                    var ok = await FlushAsync(stoppingToken);
                    nextFlushAt = DateTime.UtcNow + flushEvery;
                    if (ok)
                    {
                        _queue.CleanupRecent();
                    }
                    else
                    {
                        // Keep buffer for next retry and apply short backoff.
                        await Task.Delay(500, stoppingToken);
                    }
                    continue;
                }

                if (await _queue.Reader.WaitToReadAsync(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListenEventQueueWorker loop failed");
                await Task.Delay(500, stoppingToken);
            }
        }

        if (_buffer.Count > 0)
            _ = await FlushAsync(CancellationToken.None);
    }

    private async Task<bool> FlushAsync(CancellationToken cancellationToken)
    {
        if (_buffer.Count == 0)
            return true;

        try
        {
            var poiIds = _buffer.Select(x => x.PoiId).ToArray();
            var durations = _buffer.Select(x => x.DurationSeconds).ToArray();
            var deviceIds = _buffer.Select(x => string.IsNullOrWhiteSpace(x.DeviceId) ? null : x.DeviceId!.Trim()).ToArray();

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(cancellationToken);
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO poi_audio_listen_events (poi_id, duration_seconds, device_id)
                SELECT x.poi_id, x.duration_seconds, x.device_id
                FROM UNNEST(@PoiIds::int[], @Durations::int[], @DeviceIds::text[])
                    AS x(poi_id, duration_seconds, device_id)",
                new { PoiIds = poiIds, Durations = durations, DeviceIds = deviceIds },
                cancellationToken: cancellationToken));
            _buffer.Clear();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListenEventQueueWorker flush failed for {Count} events", _buffer.Count);
            return false;
        }
    }
}
