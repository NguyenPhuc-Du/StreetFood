using Microsoft.AspNetCore.Mvc;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/vendor")]
public class VendorMediaController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public VendorMediaController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    /// <summary>Upload ảnh từ máy (multipart). Trả về URL tuyệt đối để lưu vào DB.</summary>
    [HttpPost("media/upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> Upload([FromForm] string? username, [FromForm] string? password, IFormFile? file)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Unauthorized("Thiếu username/password.");

        if (file == null || file.Length == 0)
            return BadRequest("Chưa chọn file ảnh.");

        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var userId = await VendorAuthHelper.GetVendorUserId(connStr, username, password);
        if (userId == null)
            return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var ct = (file.ContentType ?? "").ToLowerInvariant();
        var extOk = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        var ctOk = ct is "image/jpeg" or "image/png" or "image/webp";
        if (!extOk && !ctOk)
            return BadRequest("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

        if (file.Length > 5_242_880)
            return BadRequest("Dung lượng tối đa 5MB.");

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "vendor");
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():N}{ext}";
        if (string.IsNullOrEmpty(ext))
            name += ".jpg";

        var physical = Path.Combine(dir, name);
        await using (var fs = System.IO.File.Create(physical))
        {
            await file.CopyToAsync(fs);
        }

        var rel = $"/uploads/vendor/{name}";
        var pub = _config["Api:PublicBaseUrl"]?.TrimEnd('/');
        string url;
        if (!string.IsNullOrEmpty(pub))
            url = pub + rel;
        else
            url = $"{Request.Scheme}://{Request.Host}{rel}";

        return Ok(new { url });
    }
}
