using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

public partial class AuthController
{
    const string AppRole = "app";
    const string StreetFoodToken = "StreetFood";
    const string ActivationKey30Days = "30_days_activation";

    static bool IsActivationPayload(string payload) =>
        payload.Contains(StreetFoodToken, StringComparison.OrdinalIgnoreCase)
        || payload.Contains(ActivationKey30Days, StringComparison.OrdinalIgnoreCase);

    /// <summary>Đăng ký tài khoản khách dùng app (role app).</summary>
    [HttpPost("register-app")]
    public async Task<IActionResult> RegisterApp([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
            return BadRequest("Cần tên đăng nhập và mật khẩu.");
        if (request.Username.Trim().Length < 3 || request.Password.Length < 4)
            return BadRequest("Tên đăng nhập ít nhất 3 ký tự, mật khẩu ít nhất 4 ký tự.");

        if (string.IsNullOrWhiteSpace(_connStr))
            return StatusCode(500, "Máy chủ chưa cấu hình database.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var n = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int FROM users WHERE lower(trim(username)) = lower(trim(@U))",
                new { U = request.Username });
            if (n > 0)
                return Conflict("Tên đăng nhập đã được dùng.");

            await conn.ExecuteAsync(@"
                INSERT INTO users (username, password, role)
                VALUES (@U, @P, @R)",
                new { U = request.Username.Trim(), P = request.Password, R = AppRole });

            return Ok(new { message = "Đăng ký thành công. Hãy đăng nhập." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "register-app");
            return StatusCode(500, "Lỗi khi đăng ký.");
        }
    }

    /// <summary>Đăng nhập app: chỉ role app; trả về hạn kích hoạt nếu có.</summary>
    [HttpPost("login-app")]
    public async Task<IActionResult> LoginApp([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
            return BadRequest("Cần tên đăng nhập và mật khẩu.");

        if (string.IsNullOrWhiteSpace(_connStr))
            return StatusCode(500, "Máy chủ chưa cấu hình database.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var row = await conn.QueryFirstOrDefaultAsync<AppUserDbRow>(@"
                SELECT id, username, app_activation_expires_at
                FROM users
                WHERE lower(trim(username)) = lower(trim(@U))
                  AND password = @P
                  AND COALESCE(ishidden, FALSE) = FALSE
                  AND lower(trim(role)) = @R",
                new { U = request.Username, P = request.Password, R = AppRole });

            if (row == null)
                return Unauthorized("Sai tài khoản hoặc mật khẩu.");

            return Ok(new
            {
                userId = row.id,
                username = row.username,
                activationExpiresAt = row.app_activation_expires_at.HasValue
                    ? row.app_activation_expires_at.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                    : (string?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login-app");
            return StatusCode(500, "Lỗi đăng nhập.");
        }
    }

    /// <summary>Sau khi đăng nhập: áp mã QR (StreetFood|WEEK / MONTH) lên tài khoản.</summary>
    [HttpPost("activate-app")]
    public async Task<IActionResult> ActivateApp([FromBody] ActivateAppRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password)
            || string.IsNullOrWhiteSpace(request.ActivationCode))
            return BadRequest("Cần tài khoản, mật khẩu và mã kích hoạt.");

        if (!TryParseActivationPlan(request.ActivationCode, out var days, out var planLabel, out var err))
            return BadRequest(err);

        if (string.IsNullOrWhiteSpace(_connStr))
            return StatusCode(500, "Máy chủ chưa cấu hình database.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var row = await conn.QueryFirstOrDefaultAsync<AppUserDbRow>(@"
                SELECT id, username, app_activation_expires_at
                FROM users
                WHERE lower(trim(username)) = lower(trim(@U))
                  AND password = @P
                  AND COALESCE(ishidden, FALSE) = FALSE
                  AND lower(trim(role)) = @R",
                new { U = request.Username, P = request.Password, R = AppRole });

            if (row == null)
                return Unauthorized("Sai tài khoản hoặc mật khẩu.");

            var now = DateTime.UtcNow;
            var currentExp = row.app_activation_expires_at;
            var baseTime = currentExp.HasValue && currentExp.Value > now ? currentExp.Value : now;
            var newExp = baseTime.AddDays(days);

            await conn.ExecuteAsync(@"
                UPDATE users SET app_activation_expires_at = @E WHERE id = @Id",
                new { E = newExp, Id = row.id });

            return Ok(new
            {
                activationExpiresAt = newExp.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                planLabel
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "activate-app");
            return StatusCode(500, "Lỗi kích hoạt.");
        }
    }

    static bool TryParseActivationPlan(string payload, out int days, out string planLabel, out string error)
    {
        days = 0;
        planLabel = "";
        error = "";

        payload = payload.Replace("\r", "").Replace("\n", "").Trim().Trim('\uFEFF', '\u200B', '\u200C', '\u200D');
        if (payload.Length == 0)
        {
            error = "Mã trống.";
            return false;
        }

        if (!IsActivationPayload(payload))
        {
            error = $"Mã phải chứa \"{ActivationKey30Days}\" hoặc \"{StreetFoodToken}\".";
            return false;
        }

        var u = payload.ToUpperInvariant();
        if (u.Contains("|WEEK") || u.Contains("PLAN=WEEK") || u.Contains("PLAN=1W") || u.Contains("|1W"))
        {
            days = 7;
            planLabel = "1 tuần";
            return true;
        }

        if (u.Contains("|MONTH") || u.Contains("PLAN=MONTH") || u.Contains("PLAN=1M") || u.Contains("|1M") || u.Contains("|30D"))
        {
            days = 30;
            planLabel = "1 tháng";
            return true;
        }

        // 30 ngày — chuẩn QR: StreetFood:30_days_activation:Mixi → nhãn "Mixi"
        days = 30;
        if (payload.Contains(ActivationKey30Days, StringComparison.OrdinalIgnoreCase))
        {
            var i = payload.IndexOf(ActivationKey30Days, StringComparison.OrdinalIgnoreCase);
            var afterKey = payload.AsSpan(i + ActivationKey30Days.Length);
            if (afterKey.Length > 0 && afterKey[0] == ':')
            {
                var tail = afterKey[1..].ToString().Trim();
                if (tail.Length > 0)
                {
                    var cut = tail.IndexOf(':');
                    var segment = (cut >= 0 ? tail[..cut] : tail).Trim();
                    if (segment.Length > 0)
                    {
                        planLabel = segment;
                        return true;
                    }
                }
            }

            planLabel = "30 ngày";
            return true;
        }

        planLabel = "Kích hoạt tour";
        return true;
    }
}
