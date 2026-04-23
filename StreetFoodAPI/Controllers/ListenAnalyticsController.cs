using Microsoft.AspNetCore.Mvc;
using StreetFood.API.Models;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/analytics")]
public class ListenAnalyticsController : ControllerBase
{
    private readonly ILogger<ListenAnalyticsController> _logger;
    private readonly ListenEventQueueService _queue;

    public ListenAnalyticsController(
        ILogger<ListenAnalyticsController> logger,
        ListenEventQueueService queue)
    {
        _logger = logger;
        _queue = queue;
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
            var dev = body.DeviceId?.Trim();
            if (!string.IsNullOrEmpty(dev) && dev.Length > 64)
                dev = dev[..64];

            if (_queue.IsDuplicate(body.PoiId, body.DurationSeconds, dev))
                return Ok(new { accepted = false, reason = "duplicate_window_15s" });

            await _queue.EnqueueAsync(
                new ListenEventQueueItem(body.PoiId, body.DurationSeconds, string.IsNullOrWhiteSpace(dev) ? null : dev),
                HttpContext.RequestAborted);
            return Ok(new { accepted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "poi-audio-listen");
            return StatusCode(500, "Không ghi được sự kiện.");
        }
    }
}
