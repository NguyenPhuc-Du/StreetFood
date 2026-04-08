using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly string _connStr;
    private static readonly TimeSpan VisitSpamWindow = TimeSpan.FromMinutes(5);

    public PoiController(IConfiguration config)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
    }

    static string ResolveLang(string? acceptLang)
    {
        if (string.IsNullOrWhiteSpace(acceptLang))
            return "vi";
        var raw = acceptLang.Split(',')[0].Trim().ToLowerInvariant();
        if (raw.Length < 2) return "vi";
        return raw[..2];
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
            WHERE t.LanguageCode = @Lang";

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
                WHERE p.Id = @PoiId AND t.LanguageCode = @Lang";

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
                return Ok(new { accepted = false, reason = "cooldown_5m" });
            }

            await conn.ExecuteAsync(@"
                INSERT INTO device_visits (deviceid, poiid, entertime)
                VALUES (@DeviceId, @PoiId, @EnterAt)",
                new { DeviceId = deviceId, PoiId = body.PoiId, EnterAt = enterAt });

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
                LEFT JOIN (
                    SELECT poiid, COUNT(*)::int AS cnt
                    FROM device_visits
                    WHERE entertime > NOW() - (CAST(@Days AS integer) * INTERVAL '1 day')
                    GROUP BY poiid
                ) v ON p.id = v.poiid
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