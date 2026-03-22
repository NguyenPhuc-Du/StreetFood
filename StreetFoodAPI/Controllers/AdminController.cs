using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models.Admin;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

internal sealed class ScriptRequestRow
{
    public int PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public string? NewScript { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly string _connStr;
    private readonly IConfiguration _config;
    private readonly AzureTranslatorClient _translator;
    private readonly AzureSpeechTtsService _tts;
    private readonly ILogger<AdminController> _logger;

    private static readonly string[] AllLangs = ["vi", "en", "cn", "ja", "ko"];

    public AdminController(
        IConfiguration config,
        AzureTranslatorClient translator,
        AzureSpeechTtsService tts,
        ILogger<AdminController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _config = config;
        _translator = translator;
        _tts = tts;
        _logger = logger;
    }

    private bool IsAdmin()
    {
        var expected = _config["Admin:ApiKey"];
        if (string.IsNullOrEmpty(expected))
            return true;
        if (!Request.Headers.TryGetValue("X-Admin-Key", out var key))
            return false;
        return key == expected;
    }

    private IActionResult? AdminUnauthorized()
    {
        if (!IsAdmin())
            return Unauthorized("Thiếu hoặc sai X-Admin-Key.");
        return null;
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var row = await conn.QuerySingleAsync<DashboardSummaryDto>(@"
                SELECT 
                  (SELECT COUNT(*)::int FROM pois) AS PoiCount,
                  (SELECT COUNT(*)::int FROM users WHERE role = 'vendor' AND COALESCE(ishidden, FALSE) = FALSE) AS VendorCount,
                  (SELECT COUNT(*)::int FROM script_change_requests WHERE status = 'pending') AS PendingScripts,
                  (SELECT COUNT(*)::int FROM restaurant_audio) AS AudioTracks,
                  (SELECT COUNT(*)::int FROM location_logs WHERE createdat > NOW() - INTERVAL '30 days') AS LocationSamples30d");
            return Ok(row);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("poi/{poiId:int}/regenerate-audio")]
    public async Task<IActionResult> RegenerateAudio([FromRoute] int poiId)
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            var r = await _tts.GenerateForPoiAsync(poiId);
            return Ok(r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegenerateAudio");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("analytics/heatmap")]
    public async Task<IActionResult> GetHeatmapPoints()
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<HeatPointDto>(@"
                SELECT
                    ROUND(latitude::numeric, 4)::double precision AS Lat,
                    ROUND(longitude::numeric, 4)::double precision AS Lng,
                    COUNT(*)::int AS Weight
                FROM location_logs
                WHERE createdat > NOW() - INTERVAL '90 days'
                GROUP BY ROUND(latitude::numeric, 4), ROUND(longitude::numeric, 4)
                ORDER BY Weight DESC
                LIMIT 5000");
            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "heatmap");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("analytics/paths")]
    public async Task<IActionResult> GetMovementPaths()
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<PathSegmentDto>(@"
                SELECT 
                    m.Id,
                    m.DeviceId,
                    m.FromPoiId,
                    m.ToPoiId,
                    m.CreatedAt,
                    pf.Latitude AS FromLat,
                    pf.Longitude AS FromLng,
                    pt.Latitude AS ToLat,
                    pt.Longitude AS ToLng,
                    tf.Name AS FromName,
                    tt.Name AS ToName
                FROM Movement_Paths m
                INNER JOIN POIs pf ON m.FromPoiId = pf.Id
                INNER JOIN POIs pt ON m.ToPoiId = pt.Id
                LEFT JOIN POI_Translations tf ON pf.Id = tf.PoiId AND tf.LanguageCode = 'vi'
                LEFT JOIN POI_Translations tt ON pt.Id = tt.PoiId AND tt.LanguageCode = 'vi'
                WHERE m.CreatedAt > NOW() - INTERVAL '90 days'
                ORDER BY m.CreatedAt DESC
                LIMIT 500");
            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "paths");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("poi-with-owner")]
    public async Task<IActionResult> CreatePoiWithOwner([FromBody] CreatePoiOwnerRequest body)
    {
        if (AdminUnauthorized() is { } u) return u;
        if (string.IsNullOrWhiteSpace(body.OwnerUsername) || string.IsNullOrWhiteSpace(body.OwnerPassword))
            return BadRequest("Cần username và mật khẩu chủ cửa hàng.");
        if (string.IsNullOrWhiteSpace(body.PoiName))
            return BadRequest("Cần tên POI.");

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var taken = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM Users WHERE Username = @Username",
                new { body.OwnerUsername }, tx);
            if (taken > 0)
                return BadRequest("Username đã tồn tại.");

            var userId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Users (Username, Password, Role, Email, IsHidden)
                VALUES (@Username, @Password, 'vendor', @Email, FALSE)
                RETURNING Id",
                new { body.OwnerUsername, body.OwnerPassword, Email = (string?)body.OwnerEmail }, tx);

            var poiId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO POIs (Latitude, Longitude, Address, ImageUrl, Radius)
                VALUES (@Lat, @Lng, @Address, @ImageUrl, @Radius)
                RETURNING Id",
                new
                {
                    Lat = body.Latitude,
                    Lng = body.Longitude,
                    Address = body.Address ?? "",
                    ImageUrl = body.ImageUrl ?? "",
                    Radius = body.Radius > 0 ? body.Radius : 50
                }, tx);

            foreach (var lang in AllLangs)
            {
                var desc = lang == "vi" ? (body.PoiDescription ?? "") : "";
                await conn.ExecuteAsync(@"
                    INSERT INTO POI_Translations (PoiId, LanguageCode, Name, Description)
                    VALUES (@PoiId, @Lang, @Name, @Description)",
                    new { PoiId = poiId, Lang = lang, Name = body.PoiName, Description = desc }, tx);
            }

            await conn.ExecuteAsync(@"
                INSERT INTO Restaurant_Details (PoiId, OpeningHours, Phone)
                VALUES (@PoiId, @OpeningHours, @Phone)
                ON CONFLICT (poiid) DO UPDATE SET
                    openinghours = EXCLUDED.openinghours,
                    phone = EXCLUDED.phone",
                new
                {
                    PoiId = poiId,
                    OpeningHours = body.OpeningHours ?? "",
                    Phone = body.Phone ?? ""
                }, tx);

            await conn.ExecuteAsync(@"
                INSERT INTO Restaurant_Owners (UserId, PoiId) VALUES (@UserId, @PoiId)",
                new { UserId = userId, PoiId = poiId }, tx);

            var hasScript = !string.IsNullOrWhiteSpace(body.InitialScript);
            if (hasScript)
            {
                var translated = await _translator.TranslateToLanguagesAsync(body.InitialScript!.Trim(), "vi", AllLangs);
                foreach (var kv in translated)
                {
                    await conn.ExecuteAsync(@"
                        UPDATE POI_Translations SET Description = @Desc
                        WHERE PoiId = @PoiId AND LanguageCode = @Lang",
                        new { Desc = kv.Value, PoiId = poiId, Lang = kv.Key }, tx);
                }

                await conn.ExecuteAsync(
                    "UPDATE POIs SET ScriptSubmissionState = 'approved' WHERE Id = @Id",
                    new { Id = poiId }, tx);
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE POIs SET ScriptSubmissionState = 'awaiting_vendor' WHERE Id = @Id",
                    new { Id = poiId }, tx);
            }

            await tx.CommitAsync();

            AudioGenerationResult? audio = null;
            if (hasScript)
            {
                try
                {
                    audio = await _tts.GenerateForPoiAsync(poiId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TTS sau khi tạo POI");
                    audio = new AudioGenerationResult(false, 0, ex.Message);
                }
            }

            return Ok(new
            {
                userId,
                poiId,
                scriptState = hasScript ? "approved" : "awaiting_vendor",
                audio
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "CreatePoiWithOwner");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("pois/awaiting-script")]
    public async Task<IActionResult> GetPoisAwaitingVendorScript()
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<AwaitingPoiDto>(@"
                SELECT
                    p.id AS PoiId,
                    t.name AS PoiName,
                    u.username AS OwnerUsername,
                    CASE
                        WHEN (SELECT COUNT(*)::int FROM restaurant_audio r WHERE r.poiid = p.id) >= 5 THEN 'full_audio'
                        WHEN (SELECT COUNT(*)::int FROM restaurant_audio r WHERE r.poiid = p.id) >= 1 THEN 'has_audio'
                        ELSE COALESCE(p.scriptsubmissionstate, 'awaiting_vendor')
                    END AS State,
                    (SELECT COUNT(*)::int FROM restaurant_audio r WHERE r.poiid = p.id) AS AudioCount
                FROM pois p
                INNER JOIN poi_translations t ON p.id = t.poiid AND t.languagecode = 'vi'
                INNER JOIN restaurant_owners o ON o.poiid = p.id
                INNER JOIN users u ON u.id = o.userid AND COALESCE(u.ishidden, FALSE) = FALSE
                WHERE p.scriptsubmissionstate IN ('awaiting_vendor', 'pending_review', 'approved')");
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("script-requests/pending")]
    public async Task<IActionResult> GetPendingScriptRequests()
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<PendingScriptDto>(@"
                SELECT 
                    s.Id,
                    s.PoiId,
                    s.LanguageCode,
                    s.NewScript,
                    s.Status,
                    s.CreatedAt,
                    t.Name AS PoiName,
                    u.Username AS SubmittedByUsername
                FROM Script_Change_Requests s
                INNER JOIN POI_Translations t ON s.PoiId = t.PoiId AND t.LanguageCode = 'vi'
                LEFT JOIN Users u ON s.CreatedBy = u.Id
                WHERE s.Status = 'pending'
                ORDER BY s.CreatedAt DESC");
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("script-requests/{id:int}/approve")]
    public async Task<IActionResult> ApproveScriptRequest([FromRoute] int id)
    {
        if (AdminUnauthorized() is { } u) return u;
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var req = await conn.QueryFirstOrDefaultAsync<ScriptRequestRow>(@"
                SELECT poiid AS PoiId, languagecode AS LanguageCode, newscript AS NewScript
                FROM script_change_requests WHERE id = @Id AND status = 'pending'",
                new { Id = id }, tx);

            if (req == null)
                return NotFound();

            int poiId = req.PoiId;
            string langCode = req.LanguageCode ?? "vi";
            string script = req.NewScript ?? "";

            var sourceLang = string.IsNullOrEmpty(langCode) ? "vi" : langCode;
            var translated = await _translator.TranslateToLanguagesAsync(script.Trim(), sourceLang, AllLangs);

            foreach (var kv in translated)
            {
                await conn.ExecuteAsync(@"
                    UPDATE POI_Translations SET Description = @Desc
                    WHERE PoiId = @PoiId AND LanguageCode = @Lang",
                    new { Desc = kv.Value, PoiId = poiId, Lang = kv.Key }, tx);
            }

            await conn.ExecuteAsync(
                "UPDATE Script_Change_Requests SET Status = 'approved' WHERE Id = @Id",
                new { Id = id }, tx);

            await conn.ExecuteAsync(
                "UPDATE POIs SET ScriptSubmissionState = 'approved' WHERE Id = @Id",
                new { Id = poiId }, tx);

            await tx.CommitAsync();

            AudioGenerationResult? audio = null;
            try
            {
                audio = await _tts.GenerateForPoiAsync(poiId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TTS sau khi phê duyệt script");
                audio = new AudioGenerationResult(false, 0, ex.Message);
            }

            return Ok(new
            {
                poiId,
                message = "Đã dịch và cập nhật 5 ngôn ngữ; âm thanh Azure (nếu cấu hình).",
                audio
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "ApproveScript");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("owners")]
    public async Task<IActionResult> ListOwners([FromQuery] bool includeHidden = false)
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var sql = @"
                SELECT u.Id, u.Username, u.Email, u.Role, COALESCE(u.IsHidden, FALSE) AS IsHidden,
                       p.Id AS PoiId, t.Name AS PoiName
                FROM Users u
                LEFT JOIN Restaurant_Owners o ON o.UserId = u.Id
                LEFT JOIN POIs p ON p.Id = o.PoiId
                LEFT JOIN POI_Translations t ON p.Id = t.PoiId AND t.LanguageCode = 'vi'
                WHERE u.Role = 'vendor'";

            if (!includeHidden)
                sql += " AND COALESCE(u.IsHidden, FALSE) = FALSE";

            sql += " ORDER BY u.Id";

            var rows = await conn.QueryAsync<OwnerRowDto>(sql);
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("owners/{userId:int}/hide")]
    public async Task<IActionResult> HideOwner([FromRoute] int userId)
    {
        if (AdminUnauthorized() is { } u) return u;
        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var n = await conn.ExecuteAsync(
                "UPDATE Users SET IsHidden = TRUE WHERE Id = @Id AND Role = 'vendor'",
                new { Id = userId });
            if (n == 0)
                return NotFound();
            return Ok(new { message = "Đã ẩn tài khoản (soft delete)." });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
