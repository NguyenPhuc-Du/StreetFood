using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

public partial class AuthController
{
    static readonly Regex InstallIdRegex = new(@"^[a-zA-Z0-9-]{8,64}$", RegexOptions.Compiled);

    /// <summary>Kích hoạt theo mã thiết bị (không đăng nhập). Server lưu install_id + hạn.</summary>
    [HttpPost("activate-device")]
    public async Task<IActionResult> ActivateDevice([FromBody] ActivateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.InstallId) || string.IsNullOrWhiteSpace(request.ActivationCode))
            return BadRequest("Cần installId và mã kích hoạt.");

        var installId = request.InstallId.Trim();
        if (!InstallIdRegex.IsMatch(installId))
            return BadRequest("installId không hợp lệ.");

        if (!TryParseActivationPlan(request.ActivationCode, out var days, out var planLabel, out var err))
            return BadRequest(err);

        if (string.IsNullOrWhiteSpace(_connStr))
            return StatusCode(500, "Máy chủ chưa cấu hình database.");

        try
        {
            using var lease = await _userIngressQueue.EnterAsync($"install:{installId}", HttpContext.RequestAborted);
            await using var conn = new NpgsqlConnection(_connStr);
            var currentExp = await conn.QueryFirstOrDefaultAsync<DateTime?>(@"
                SELECT expires_at FROM device_activations WHERE install_id = @I",
                new { I = installId });

            var now = DateTime.UtcNow;
            var baseTime = currentExp.HasValue && currentExp.Value > now ? currentExp.Value : now;
            var newExp = baseTime.AddDays(days);

            await conn.ExecuteAsync(@"
                INSERT INTO device_activations (install_id, expires_at, plan_label, updated_at)
                VALUES (@I, @E, @P, NOW())
                ON CONFLICT (install_id) DO UPDATE SET
                    expires_at = EXCLUDED.expires_at,
                    plan_label = EXCLUDED.plan_label,
                    updated_at = NOW()",
                new { I = installId, E = newExp, P = planLabel });

            return Ok(new
            {
                activationExpiresAt = newExp.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                planLabel
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "activate-device");
            return StatusCode(500, "Lỗi kích hoạt thiết bị.");
        }
    }

    /// <summary>Tra cứu trạng thái kích hoạt theo install_id (đồng bộ khi app có mạng).</summary>
    [HttpGet("device-status")]
    public async Task<IActionResult> GetDeviceStatus([FromQuery] string? installId)
    {
        if (string.IsNullOrWhiteSpace(installId))
            return BadRequest("Thiếu installId.");
        var id = installId.Trim();
        if (!InstallIdRegex.IsMatch(id))
            return BadRequest("installId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(_connStr))
            return StatusCode(500, "Máy chủ chưa cấu hình database.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var row = await conn.QueryFirstOrDefaultAsync<DeviceActivationRow>(@"
                SELECT expires_at, plan_label
                FROM device_activations
                WHERE install_id = @I",
                new { I = id });

            if (row == null)
                return Ok(new { active = false, activationExpiresAt = (string?)null, planLabel = (string?)null });

            var exp = row.expires_at;
            var plan = row.plan_label;
            var active = exp > DateTime.UtcNow;
            return Ok(new
            {
                active,
                activationExpiresAt = exp.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                planLabel = plan
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "device-status");
            return StatusCode(500, "Lỗi tra cứu.");
        }
    }
}
