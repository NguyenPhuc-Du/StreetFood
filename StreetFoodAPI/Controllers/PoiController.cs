using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using StreetFood.API.Models;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly string _connStr;
    private static readonly TimeSpan VisitSpamWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MovementSpamWindow = TimeSpan.FromMinutes(2);
    private readonly PoiIngressQueueService _poiIngressQueue;

    public PoiController(IConfiguration config, PoiIngressQueueService poiIngressQueue)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _poiIngressQueue = poiIngressQueue;
    }

    static string ResolveLang(string? acceptLang)
    {
        if (string.IsNullOrWhiteSpace(acceptLang))
            return "vi";
        var raw = acceptLang.Split(',')[0].Trim().ToLowerInvariant();
        if (raw.Length < 2) return "vi";
        var code = raw[..2];
        if (code == "zh") return "cn";
        return code;
    }

    [HttpGet]
    public async Task<IActionResult> GetPois()
    {
        try
        {
            var lang = ResolveLang(Request.Headers["Accept-Language"].ToString());

            using var conn = new NpgsqlConnection(_connStr);

            // SQL cập nhật: Lấy thêm OpeningHours từ Restaurant_Details
            // Sửa lại SQL trong hàm GetPois
            var sql = @"
            SELECT 
                p.Id, p.Latitude, p.Longitude, COALESCE(p.Radius, 50) AS Radius, p.ImageUrl, 
                t.Name,
                p.address AS Address,
                t.Description AS Description,
                d.OpeningHours,
                a.AudioUrl
            FROM POIs p
            INNER JOIN POI_Translations t ON p.Id = t.PoiId
            LEFT JOIN Restaurant_Details d ON p.Id = d.PoiId
            LEFT JOIN Restaurant_Audio a ON p.Id = a.PoiId AND a.LanguageCode = @Lang
            LEFT JOIN Restaurant_Owners o ON p.Id = o.PoiId
            LEFT JOIN Users u ON o.UserId = u.Id
            WHERE t.LanguageCode = @Lang
              AND (u.Id IS NULL OR COALESCE(u.IsHidden, FALSE) = FALSE)";

            var list = (await conn.QueryAsync<PoiDto>(sql, new { Lang = lang })).ToList();

            if (!list.Any() && lang != "vi")
                list = (await conn.QueryAsync<PoiDto>(sql, new { Lang = "vi" })).ToList();

            return Ok(list);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPoiDetail([FromRoute] int id)
    {
        try
        {
            var lang = ResolveLang(Request.Headers["Accept-Language"].ToString());

            using var conn = new NpgsqlConnection(_connStr);

            var detailSql = @"
                SELECT 
                    p.Id, p.Latitude, p.Longitude, p.ImageUrl,
                    t.Name,
                    p.address AS Address,
                    t.Description AS Description,
                    d.OpeningHours, d.Phone,
                    a.AudioUrl
                FROM POIs p
                INNER JOIN POI_Translations t ON p.Id = t.PoiId
                LEFT JOIN Restaurant_Details d ON p.Id = d.PoiId
                LEFT JOIN Restaurant_Audio a ON p.Id = a.PoiId AND a.LanguageCode = @Lang
                LEFT JOIN Restaurant_Owners o ON p.Id = o.PoiId
                LEFT JOIN Users u ON o.UserId = u.Id
                WHERE p.Id = @PoiId AND t.LanguageCode = @Lang
                  AND (u.Id IS NULL OR COALESCE(u.IsHidden, FALSE) = FALSE)";

            var detail = (await conn.QueryAsync<PoiDetailDto>(detailSql, new { PoiId = id, Lang = lang })).FirstOrDefault();

            // fallback to Vietnamese when translation missing
            if (detail == null && lang != "vi")
            {
                detail = (await conn.QueryAsync<PoiDetailDto>(detailSql, new { PoiId = id, Lang = "vi" })).FirstOrDefault();
            }

            if (detail == null) return NotFound();

            List<FoodDto> foods;
            try
            {
                var foodsSql = @"
                    SELECT Id, PoiId, Name, Description, Price, ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId
                      AND COALESCE(IsHidden, FALSE) = FALSE";

                foods = (await conn.QueryAsync<FoodDto>(foodsSql, new { PoiId = id })).ToList();
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                // Migration drift safety: column missing => fallback to showing all foods.
                var foodsSql = @"
                    SELECT Id, PoiId, Name, Description, Price, ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId";

                foods = (await conn.QueryAsync<FoodDto>(foodsSql, new { PoiId = id })).ToList();
            }
            detail.Foods = foods;

            return Ok(detail);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Ghi nhận một lượt "vào quán" theo thiết bị, chống spam trong 5 phút cho cùng device + POI.
    /// </summary>
    [HttpPost("visit")]
    public async Task<IActionResult> TrackVisit([FromBody] TrackVisitRequest? body)
    {
        if (body == null || body.PoiId <= 0 || string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest("Thiếu poiId hoặc deviceId.");

        var deviceId = body.DeviceId.Trim();
        if (deviceId.Length > 100) deviceId = deviceId[..100];

        var enterAt = body.EnteredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (enterAt > DateTime.UtcNow.AddMinutes(1))
            enterAt = DateTime.UtcNow;

        try
        {
            using var lease = await _poiIngressQueue.EnterAsync(body.PoiId, HttpContext.RequestAborted);
            await using var conn = new NpgsqlConnection(_connStr);
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM pois WHERE id = @Id)",
                new { Id = body.PoiId });
            if (!exists)
                return BadRequest("POI không tồn tại.");

            var last = await conn.QueryFirstOrDefaultAsync<DateTime?>(@"
                SELECT entertime
                FROM device_visits
                WHERE poiid = @PoiId AND deviceid = @DeviceId
                ORDER BY entertime DESC
                LIMIT 1", new { PoiId = body.PoiId, DeviceId = deviceId });

            if (last.HasValue && (enterAt - last.Value.ToUniversalTime()) < VisitSpamWindow)
            {
                return Ok(new { accepted = false, reason = "cooldown_5m", queueDelayMs = lease.TotalDelayMs });
            }

            await conn.ExecuteAsync(@"
                INSERT INTO device_visits (deviceid, poiid, entertime)
                VALUES (@DeviceId, @PoiId, @EnterAt)",
                new { DeviceId = deviceId, PoiId = body.PoiId, EnterAt = enterAt });

            return Ok(new { accepted = true, queueDelayMs = lease.TotalDelayMs });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Bắt đầu một phiên vào quán (thiết bị vào vùng POI).</summary>
    [HttpPost("visit/start")]
    public async Task<IActionResult> StartVisit([FromBody] VisitSessionRequest? body)
    {
        if (body == null || body.PoiId <= 0 || string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest("Thiếu poiId hoặc deviceId.");

        var deviceId = body.DeviceId.Trim();
        if (deviceId.Length > 100) deviceId = deviceId[..100];
        var enterAt = body.AtUtc?.ToUniversalTime() ?? DateTime.UtcNow;

        try
        {
            using var lease = await _poiIngressQueue.EnterAsync(body.PoiId, HttpContext.RequestAborted);
            await using var conn = new NpgsqlConnection(_connStr);
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM pois WHERE id = @Id)",
                new { Id = body.PoiId });
            if (!exists) return BadRequest("POI không tồn tại.");

            var open = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int
                FROM device_visits
                WHERE deviceid = @D AND poiid = @P AND exittime IS NULL",
                new { D = deviceId, P = body.PoiId });
            if (open > 0) return Ok(new { accepted = false, reason = "already_open", queueDelayMs = lease.TotalDelayMs });

            var latestExit = await conn.QueryFirstOrDefaultAsync<DateTime?>(@"
                SELECT exittime
                FROM device_visits
                WHERE deviceid = @D AND poiid = @P AND exittime IS NOT NULL
                ORDER BY exittime DESC
                LIMIT 1",
                new { D = deviceId, P = body.PoiId });
            if (latestExit.HasValue && (enterAt - latestExit.Value.ToUniversalTime()) < VisitSpamWindow)
                return Ok(new { accepted = false, reason = "cooldown_5m", queueDelayMs = lease.TotalDelayMs });

            await conn.ExecuteAsync(@"
                INSERT INTO device_visits (deviceid, poiid, entertime)
                VALUES (@D, @P, @T)",
                new { D = deviceId, P = body.PoiId, T = enterAt });

            return Ok(new { accepted = true, queueDelayMs = lease.TotalDelayMs });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Kết thúc phiên vào quán (thiết bị ra vùng POI), cập nhật exittime + duration.</summary>
    [HttpPost("visit/end")]
    public async Task<IActionResult> EndVisit([FromBody] VisitSessionRequest? body)
    {
        if (body == null || body.PoiId <= 0 || string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest("Thiếu poiId hoặc deviceId.");

        var deviceId = body.DeviceId.Trim();
        if (deviceId.Length > 100) deviceId = deviceId[..100];
        var exitAt = body.AtUtc?.ToUniversalTime() ?? DateTime.UtcNow;

        try
        {
            using var lease = await _poiIngressQueue.EnterAsync(body.PoiId, HttpContext.RequestAborted);
            await using var conn = new NpgsqlConnection(_connStr);
            var id = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT id
                FROM device_visits
                WHERE deviceid = @D AND poiid = @P AND exittime IS NULL
                ORDER BY entertime DESC
                LIMIT 1",
                new { D = deviceId, P = body.PoiId });
            if (!id.HasValue)
                return Ok(new { accepted = false, reason = "no_open_session", queueDelayMs = lease.TotalDelayMs });

            await conn.ExecuteAsync(@"
                UPDATE device_visits
                SET exittime = @ExitAt,
                    duration = GREATEST(0, EXTRACT(EPOCH FROM (@ExitAt - entertime))::int)
                WHERE id = @Id",
                new { ExitAt = exitAt, Id = id.Value });

            return Ok(new { accepted = true, queueDelayMs = lease.TotalDelayMs });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Ghi log vị trí thô để heatmap.</summary>
    [HttpPost("log")]
    public async Task<IActionResult> LogLocation([FromBody] LocationLogRequest? body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest("Thiếu deviceId.");
        if (body.Latitude is < -90 or > 90 || body.Longitude is < -180 or > 180)
            return BadRequest("Tọa độ không hợp lệ.");

        var deviceId = body.DeviceId.Trim();
        if (deviceId.Length > 100) deviceId = deviceId[..100];

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.ExecuteAsync(@"
                INSERT INTO location_logs (deviceid, latitude, longitude, createdat)
                VALUES (@D, @Lat, @Lng, NOW())",
                new { D = deviceId, Lat = body.Latitude, Lng = body.Longitude });
            return Ok(new { accepted = true });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Ghi nhận dịch chuyển giữa 2 POI liên tiếp.</summary>
    [HttpPost("movement")]
    public async Task<IActionResult> TrackMovement([FromBody] MovementPathRequest? body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.DeviceId))
            return BadRequest("Thiếu deviceId.");
        if (body.FromPoiId <= 0 || body.ToPoiId <= 0 || body.FromPoiId == body.ToPoiId)
            return BadRequest("fromPoiId/toPoiId không hợp lệ.");

        var deviceId = body.DeviceId.Trim();
        if (deviceId.Length > 100) deviceId = deviceId[..100];
        var at = body.AtUtc?.ToUniversalTime() ?? DateTime.UtcNow;

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var exists = await conn.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS(SELECT 1 FROM pois WHERE id = @F)
                AND EXISTS(SELECT 1 FROM pois WHERE id = @T)",
                new { F = body.FromPoiId, T = body.ToPoiId });
            if (!exists) return BadRequest("POI không tồn tại.");

            var last = await conn.QueryFirstOrDefaultAsync<(int FromPoiId, int ToPoiId, DateTime CreatedAt)?>(@"
                SELECT frompoiid AS FromPoiId, topoiid AS ToPoiId, createdat AS CreatedAt
                FROM movement_paths
                WHERE deviceid = @D
                ORDER BY createdat DESC
                LIMIT 1", new { D = deviceId });

            if (last.HasValue
                && last.Value.FromPoiId == body.FromPoiId
                && last.Value.ToPoiId == body.ToPoiId
                && (at - last.Value.CreatedAt.ToUniversalTime()) < MovementSpamWindow)
            {
                return Ok(new { accepted = false, reason = "cooldown_2m" });
            }

            await conn.ExecuteAsync(@"
                INSERT INTO movement_paths (deviceid, frompoiid, topoiid, createdat)
                VALUES (@D, @F, @T, @At)",
                new { D = deviceId, F = body.FromPoiId, T = body.ToPoiId, At = at });
            return Ok(new { accepted = true });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Top quán theo số lượt vào (device_visits) trong N ngày gần nhất.
    /// </summary>
    [HttpGet("top")]
    public async Task<IActionResult> GetTopPois([FromQuery] int top = 10, [FromQuery] int days = 30)
    {
        top = Math.Clamp(top, 1, 20);
        days = Math.Clamp(days, 1, 365);
        var lang = ResolveLang(Request.Headers["Accept-Language"].ToString());

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var sql = @"
                SELECT
                    p.id,
                    t.name,
                    p.latitude,
                    p.longitude,
                    COALESCE(p.radius, 50) AS radius,
                    p.imageurl,
                    p.address,
                    t.description,
                    d.openinghours,
                    a.audiourl,
                    COALESCE(v.cnt, 0) AS visitcount
                FROM pois p
                INNER JOIN poi_translations t ON p.id = t.poiid AND t.languagecode = @Lang
                LEFT JOIN restaurant_details d ON p.id = d.poiid
                LEFT JOIN restaurant_audio a ON p.id = a.poiid AND a.languagecode = @Lang
                LEFT JOIN restaurant_owners o ON p.id = o.poiid
                LEFT JOIN users u ON o.userid = u.id
                LEFT JOIN (
                    SELECT poiid, COUNT(*)::int AS cnt
                    FROM device_visits
                    WHERE entertime > NOW() - (CAST(@Days AS integer) * INTERVAL '1 day')
                    GROUP BY poiid
                ) v ON p.id = v.poiid
                WHERE (u.id IS NULL OR COALESCE(u.ishidden, FALSE) = FALSE)
                ORDER BY COALESCE(v.cnt, 0) DESC, p.id
                LIMIT @Top";

            var list = (await conn.QueryAsync<TopPoiDto>(sql, new { Lang = lang, Top = top, Days = days })).ToList();
            if (!list.Any() && lang != "vi")
                list = (await conn.QueryAsync<TopPoiDto>(sql, new { Lang = "vi", Top = top, Days = days })).ToList();

            return Ok(list);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Điểm ưu tiên heatmap theo POI (dùng để resolve khi user đứng trong nhiều POI).
    /// Tính từ số sample location_logs rơi vào bán kính của từng POI trong N ngày.
    /// </summary>
    [HttpGet("heat-priority")]
    public async Task<IActionResult> GetPoiHeatPriority([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 180);
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<PoiHeatPriorityDto>(@"
                SELECT
                    p.id AS PoiId,
                    COUNT(l.deviceid)::int AS HeatScore
                FROM pois p
                LEFT JOIN location_logs l
                    ON l.createdat > NOW() - (CAST(@Days AS integer) * INTERVAL '1 day')
                    AND (
                        6371000 * acos(
                            LEAST(
                                1.0,
                                GREATEST(
                                    -1.0,
                                    cos(radians(p.latitude)) * cos(radians(l.latitude))
                                    * cos(radians(l.longitude) - radians(p.longitude))
                                    + sin(radians(p.latitude)) * sin(radians(l.latitude))
                                )
                            )
                        )
                    ) <= COALESCE(p.radius, 50)
                GROUP BY p.id
                ORDER BY HeatScore DESC, p.id");

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public sealed class TrackVisitRequest
{
    public int PoiId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime? EnteredAtUtc { get; set; }
}

public sealed class TopPoiDto : PoiDto
{
    public int VisitCount { get; set; }
}

public sealed class VisitSessionRequest
{
    public int PoiId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime? AtUtc { get; set; }
}

public sealed class LocationLogRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class MovementPathRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public int FromPoiId { get; set; }
    public int ToPoiId { get; set; }
    public DateTime? AtUtc { get; set; }
}

public sealed class PoiHeatPriorityDto
{
    public int PoiId { get; set; }
    public int HeatScore { get; set; }
}