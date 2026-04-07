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

            await conn.ExecuteAsync(@"
                INSERT INTO poi_audio_listen_events (poi_id, duration_seconds, device_id)
                VALUES (@PoiId, @Sec, @Dev)",
                new { PoiId = body.PoiId, Sec = body.DurationSeconds, Dev = string.IsNullOrEmpty(dev) ? null : dev });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "poi-audio-listen");
            return StatusCode(500, "Không ghi được sự kiện.");
        }
    }
}
