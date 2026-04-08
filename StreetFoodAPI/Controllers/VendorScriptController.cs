using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

/// <summary>
/// Gọi từ Web Vendor khi chủ cửa hàng gửi script chờ phê duyệt.
/// </summary>
[ApiController]
[Route("api/vendor")]
public class VendorScriptController : ControllerBase
{
    private static readonly string[] AllLangs = ["vi", "en", "cn", "ja", "ko"];

    private readonly string _connStr;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly R2StorageService _r2;
    private readonly ILogger<VendorScriptController> _logger;

    public VendorScriptController(
        IConfiguration config,
        IWebHostEnvironment env,
        R2StorageService r2,
        ILogger<VendorScriptController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _config = config;
        _env = env;
        _r2 = r2;
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

    /// <summary>Upload đủ 5 file MP3 (vi,en,cn,ja,ko); lưu JSON vào script_change_requests để admin chỉ việc phê duyệt.</summary>
    [HttpPost("submit-audio-bundle")]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> SubmitAudioBundle(
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] int poiId,
        IFormFile? audio_vi,
        IFormFile? audio_en,
        IFormFile? audio_cn,
        IFormFile? audio_ja,
        IFormFile? audio_ko)
    {
        if (poiId <= 0) return BadRequest("POIId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Unauthorized("Thiếu username/password.");

        var files = new Dictionary<string, IFormFile?>
        {
            ["vi"] = audio_vi,
            ["en"] = audio_en,
            ["cn"] = audio_cn,
            ["ja"] = audio_ja,
            ["ko"] = audio_ko
        };

        foreach (var kv in files)
        {
            if (kv.Value == null || kv.Value.Length == 0)
                return BadRequest($"Thiếu file âm thanh cho ngôn ngữ: {kv.Key}.");
        }

        const long maxEach = 12_582_912;
        foreach (var kv in files)
        {
            var f = kv.Value!;
            if (f.Length > maxEach)
                return BadRequest($"File {kv.Key} vượt quá 12MB.");

            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            var ct = (f.ContentType ?? "").ToLowerInvariant();
            var extOk = ext is ".mp3" or ".mpeg" or ".m4a" or ".wav" or ".webm" or ".ogg";
            var ctOk = ct is "audio/mpeg" or "audio/mp3" or "audio/mp4" or "audio/x-m4a" or "audio/wav"
                or "audio/x-wav" or "audio/webm" or "audio/ogg" or "application/octet-stream";
            if (!extOk && !ctOk)
                return BadRequest($"Định dạng không hỗ trợ cho {kv.Key} (dùng MP3/WAV/WEBM/OGG).");
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var userId = await VendorAuthHelper.GetVendorUserId(_connStr, username, password);
            if (userId == null)
                return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

            var owns = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int FROM restaurant_owners WHERE userid = @U AND poiid = @P",
                new { U = userId.Value, P = poiId });

            if (owns == 0)
                return BadRequest("Bạn không quản lý POI này.");

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(webRoot, "uploads", "vendor", "audio");
            if (!_r2.IsEnabled)
                Directory.CreateDirectory(dir);

            var urlsObj = new Dictionary<string, string>();
            foreach (var lang in AllLangs)
            {
                var f = files[lang]!;
                var ext = Path.GetExtension(f.FileName);
                if (string.IsNullOrEmpty(ext) || ext == ".")
                    ext = ".mp3";
                var name = $"{poiId}_{lang}_{Guid.NewGuid():N}{ext}";
                string url;
                if (_r2.IsEnabled)
                {
                    await using var input = f.OpenReadStream();
                    var objectKey = $"{poiId}/audio/{lang}{ext}";
                    var uploaded = await _r2.UploadAsync(objectKey, input, f.ContentType ?? "audio/mpeg");
                    if (string.IsNullOrWhiteSpace(uploaded))
                        return BadRequest("Upload Cloudflare R2 thất bại. Kiểm tra cấu hình Storage:R2.");
                    url = uploaded;
                }
                else
                {
                    var physical = Path.Combine(dir, name);
                    await using (var fs = System.IO.File.Create(physical))
                    {
                        await f.CopyToAsync(fs);
                    }
                    var rel = $"/uploads/vendor/audio/{name}";
                    var pub = _config["Api:PublicBaseUrl"]?.TrimEnd('/');
                    url = !string.IsNullOrEmpty(pub)
                        ? pub + rel
                        : $"{Request.Scheme}://{Request.Host}{rel}";
                }
                urlsObj[lang] = url;
            }

            var payload = JsonSerializer.Serialize(new
            {
                type = "vendor_audio_bundle",
                urls = urlsObj
            });

            await conn.ExecuteAsync(@"
                INSERT INTO script_change_requests (poiid, languagecode, newscript, status, createdby)
                VALUES (@PoiId, 'bndl', @Script, 'pending', @CreatedBy)",
                new { PoiId = poiId, Script = payload, CreatedBy = userId.Value });

            await conn.ExecuteAsync(
                @"UPDATE pois SET scriptsubmissionstate = 'pending_review' WHERE id = @Id",
                new { Id = poiId });

            return Ok(new { message = "Đã gửi gói 5 file âm thanh. Chờ admin phê duyệt." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitAudioBundle");
            return BadRequest(ex.Message);
        }
    }
}
