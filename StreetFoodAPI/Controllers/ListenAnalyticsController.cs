using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/analytics")]
public class ListenAnalyticsController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<ListenAnalyticsController> _logger;
    private static readonly TimeSpan ListenDuplicateWindow = TimeSpan.FromSeconds(15);

    public ListenAnalyticsController(IConfiguration config, ILogger<ListenAnalyticsController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
    }

    /// <summary>App mobile gửi sau mỗi phiên nghe (tổng giây thực tế đang phát stream).</summary>
    [HttpPost("poi-audio-listen")]
    public async Task<IActionResult> PostListenEvent([FromBody] PoiListenEventRequest? body)
    {
        if (body == null || body.PoiId <= 0)
            return BadRequest("poiId không hợp lệ.");
        if (body.DurationSeconds < 3 || body.DurationSeconds > 7200)
            return BadRequest("durationSeconds phải từ 3 đến 7200.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM pois WHERE id = @Id)",
                new { Id = body.PoiId });
            if (!exists)
                return BadRequest("POI không tồn tại.");

            var dev = body.DeviceId?.Trim();
            if (!string.IsNullOrEmpty(dev) && dev.Length > 64)
                dev = dev[..64];

            // De-duplicate retries from unstable networks: same device + poi + duration in a short window.
            if (!string.IsNullOrEmpty(dev))
            {
                var latest = await conn.QueryFirstOrDefaultAsync<(int Duration, DateTime CreatedAt)?>(@"
                    SELECT duration_seconds AS Duration, created_at AS CreatedAt
                    FROM poi_audio_listen_events
                    WHERE poi_id = @PoiId AND device_id = @Dev
                    ORDER BY created_at DESC
                    LIMIT 1",
                    new { PoiId = body.PoiId, Dev = dev });

                if (latest.HasValue
                    && Math.Abs(latest.Value.Duration - body.DurationSeconds) <= 2
                    && (DateTime.UtcNow - latest.Value.CreatedAt.ToUniversalTime()) < ListenDuplicateWindow)
                {
                    return Ok(new { accepted = false, reason = "duplicate_window_15s" });
                }
            }

            await conn.ExecuteAsync(@"
                INSERT INTO poi_audio_listen_events (poi_id, duration_seconds, device_id)
                VALUES (@PoiId, @Sec, @Dev)",
                new { PoiId = body.PoiId, Sec = body.DurationSeconds, Dev = string.IsNullOrEmpty(dev) ? null : dev });

            return Ok(new { accepted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "poi-audio-listen");
            return StatusCode(500, "Không ghi được sự kiện.");
        }
    }
}
