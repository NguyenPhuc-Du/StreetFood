using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace StreetFood.API.Controllers;

/// <summary>
/// Gọi từ Web Vendor khi chủ cửa hàng gửi script chờ phê duyệt.
/// </summary>
[ApiController]
[Route("api/vendor")]
public class VendorScriptController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<VendorScriptController> _logger;

    public VendorScriptController(IConfiguration config, ILogger<VendorScriptController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
    }

    public record SubmitScriptBody(string Username, string Password, int PoiId, string ScriptText, string LanguageCode = "vi");

    [HttpPost("submit-script")]
    public async Task<IActionResult> SubmitScript([FromBody] SubmitScriptBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(body.ScriptText))
            return BadRequest("Script trống.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var userId = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT id FROM users 
                WHERE username = @U AND password = @P AND role = 'vendor' AND COALESCE(ishidden, FALSE) = FALSE",
                new { U = body.Username, P = body.Password });

            if (userId == null)
                return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

            var owns = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int FROM restaurant_owners WHERE userid = @U AND poiid = @P",
                new { U = userId.Value, P = body.PoiId });

            if (owns == 0)
                return BadRequest("Bạn không quản lý POI này.");

            await conn.ExecuteAsync(@"
                INSERT INTO script_change_requests (poiid, languagecode, newscript, status, createdby)
                VALUES (@PoiId, @Lang, @Script, 'pending', @CreatedBy)",
                new
                {
                    body.PoiId,
                    Lang = body.LanguageCode ?? "vi",
                    Script = body.ScriptText.Trim(),
                    CreatedBy = userId.Value
                });

            await conn.ExecuteAsync(
                @"UPDATE pois SET scriptsubmissionstate = 'pending_review' WHERE id = @Id",
                new { Id = body.PoiId });

            return Ok(new { message = "Đã gửi script. Chờ admin phê duyệt." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitScript");
            return BadRequest(ex.Message);
        }
    }
}
