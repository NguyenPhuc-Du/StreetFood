using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/vendor")]
public class VendorMediaController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly R2StorageService _r2;

    public VendorMediaController(IConfiguration config, IWebHostEnvironment env, R2StorageService r2)
    {
        _config = config;
        _env = env;
        _r2 = r2;
    }

    /// <summary>Upload ảnh từ máy (multipart). Trả về URL tuyệt đối để lưu vào DB.</summary>
    [HttpPost("media/upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> Upload([FromForm] string? username, [FromForm] string? password, IFormFile? file, [FromForm] string? purpose = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Unauthorized("Thiếu username/password.");

        if (file == null || file.Length == 0)
            return BadRequest("Chưa chọn file ảnh.");

        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var userId = await VendorAuthHelper.GetVendorUserId(connStr, username, password);
        if (userId == null)
            return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

        await using var conn = new NpgsqlConnection(connStr);
        var poiId = await conn.QueryFirstOrDefaultAsync<int?>(@"
            SELECT poiid
            FROM restaurant_owners
            WHERE userid = @U
            ORDER BY poiid
            LIMIT 1",
            new { U = userId.Value });
        if (poiId == null || poiId.Value <= 0)
            return BadRequest("Không tìm thấy POI của vendor.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var ct = (file.ContentType ?? "").ToLowerInvariant();
        var extOk = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        var ctOk = ct is "image/jpeg" or "image/png" or "image/webp";
        if (!extOk && !ctOk)
            return BadRequest("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

        if (file.Length > 5_242_880)
            return BadRequest("Dung lượng tối đa 5MB.");

        var bucketFolder = string.Equals((purpose ?? "").Trim(), "dishes", StringComparison.OrdinalIgnoreCase)
            ? "dishes"
            : "images";
        var normalizedExt = string.IsNullOrWhiteSpace(ext) ? ".png" : ext;
        string name;
        if (bucketFolder == "images")
        {
            // Store logo/shop image in a stable key for each POI.
            name = "main.png";
        }
        else if (bucketFolder == "dishes")
        {
            var nextIndex = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int
                FROM foods
                WHERE poiid = @P", new { P = poiId.Value }) + 1;
            name = nextIndex + normalizedExt;
        }
        else
        {
            name = $"{Guid.NewGuid():N}{normalizedExt}";
        }

        var rel = $"/uploads/vendor/{bucketFolder}/{name}";
        var pub = _config["Api:PublicBaseUrl"]?.TrimEnd('/');

        if (_r2.IsEnabled)
        {
            await using var input = file.OpenReadStream();
            var objectKey = $"{poiId.Value}/{bucketFolder}/{name}";
            var r2Url = await _r2.UploadAsync(objectKey, input, file.ContentType ?? "image/jpeg", HttpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(r2Url))
                return Ok(new { url = r2Url });
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "vendor", bucketFolder);
        Directory.CreateDirectory(dir);
        var physical = Path.Combine(dir, name);
        await using (var fs = System.IO.File.Create(physical))
        {
            await file.CopyToAsync(fs);
        }

        string url;
        if (!string.IsNullOrEmpty(pub))
            url = pub + rel;
        else
            url = $"{Request.Scheme}://{Request.Host}{rel}";

        return Ok(new { url });
    }
}
