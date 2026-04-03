using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
    }

    /// <summary>Đăng nhập: kiểm tra bảng users (username, password, role admin|vendor, không bị ẩn).</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
            return BadRequest("Cần tên đăng nhập và mật khẩu.");

        if (string.IsNullOrWhiteSpace(_connStr))
        {
            _logger.LogError("Auth login: DefaultConnection chưa cấu hình.");
            return StatusCode(500, "Máy chủ chưa cấu hình kết nối database.");
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var role = await conn.QueryFirstOrDefaultAsync<string?>(@"
                SELECT lower(trim(role))
                FROM users
                WHERE lower(trim(username)) = lower(trim(@U))
                  AND password = @P
                  AND COALESCE(ishidden, FALSE) = FALSE
                  AND lower(trim(role)) IN ('admin', 'vendor')",
                new { U = request.Username, P = request.Password });

            if (string.IsNullOrEmpty(role))
                return Unauthorized("Sai tài khoản hoặc mật khẩu");

            return Ok(new { role, message = "Login success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth login DB error");
            return StatusCode(500, "Lỗi kết nối hoặc truy vấn database.");
        }
    }
}
