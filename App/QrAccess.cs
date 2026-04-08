using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace App;

/// <summary>
/// Mã QR kích hoạt trong app — lưu hạn trên máy (Preferences), không cần DB. Mặc định 7 ngày; có thể |WEEK / |MONTH (30 ngày). Không mở trình duyệt từ nội dung QR.
/// Chấp nhận một trong các dạng nội dung:
/// <list type="bullet">
/// <item><description>Chuẩn in ấn: <c>StreetFood:30_days_activation:Mixi</c></description></item>
/// <item><description><c>30_days_activation...</c> (ví dụ <c>30_days_activation_key_mixi_2023</c>)</description></item>
/// <item><description><c>StreetFood</c> (mặc định 7 ngày; tùy chọn <c>|WEEK</c> / <c>|MONTH</c>)</description></item>
/// </list>
/// </summary>
public static class QrAccess
{
    public const string RequiredToken = "StreetFood";
    const string JwtIssuer = "StreetFood";
    const string JwtActivationType = "activation";
    const string JwtSecret = "StreetFood_QR_JWT_HS256_2026";

    /// <summary>Chuỗi trong QR tour Mixi / kích hoạt 30 ngày cố định.</summary>
    public const string ActivationKey30Days = "30_days_activation";

    /// <summary>Mã QR phát hành chính thức (nội dung plain text trong mã).</summary>
    public const string OfficialActivationPayload = "StreetFood:30_days_activation:Mixi";

    public static string NormalizePayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return "";
        var s = payload.Replace("\r", "").Replace("\n", "").Trim().Trim('\uFEFF', '\u200B', '\u200C', '\u200D');
        return s;
    }

    public static bool IsActivationPayload(string? payload)
    {
        var p = NormalizePayload(payload);
        if (p.Length == 0) return false;
        return p.Contains(RequiredToken, StringComparison.OrdinalIgnoreCase)
               || p.Contains(ActivationKey30Days, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseActivation(string? payload, out QrActivationPlan plan, out string? errorMessage)
    {
        plan = QrActivationPlan.None;
        errorMessage = null;

        payload = NormalizePayload(payload);
        if (payload.Length == 0)
        {
            errorMessage = "Mã trống.";
            return false;
        }

        if (TryParseJwtActivation(payload, out plan, out errorMessage))
            return true;

        if (!IsActivationPayload(payload))
        {
            errorMessage = $"Mã phải chứa \"{ActivationKey30Days}\" hoặc \"{RequiredToken}\".";
            return false;
        }

        var u = payload.ToUpperInvariant();
        if (u.Contains("|WEEK") || u.Contains("PLAN=WEEK") || u.Contains("PLAN=1W") || u.Contains("|1W"))
        {
            plan = QrActivationPlan.Week;
            return true;
        }

        if (u.Contains("|MONTH") || u.Contains("PLAN=MONTH") || u.Contains("PLAN=1M") || u.Contains("|1M") || u.Contains("|30D"))
        {
            plan = QrActivationPlan.Month;
            return true;
        }

        plan = QrActivationPlan.Standard;
        return true;
    }

    static bool TryParseJwtActivation(string payload, out QrActivationPlan plan, out string? error)
    {
        plan = QrActivationPlan.None;
        error = null;
        var parts = payload.Split('.');
        if (parts.Length != 3) return false;

        try
        {
            var signingInput = $"{parts[0]}.{parts[1]}";
            var expectedSig = ComputeBase64UrlHmacSha256(signingInput, JwtSecret);
            if (!string.Equals(parts[2], expectedSig, StringComparison.Ordinal))
            {
                error = "JWT chữ ký không hợp lệ.";
                return false;
            }

            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            using var header = JsonDocument.Parse(headerJson);
            var alg = header.RootElement.TryGetProperty("alg", out var algEl) ? algEl.GetString() : null;
            if (!string.Equals(alg, "HS256", StringComparison.OrdinalIgnoreCase))
            {
                error = "JWT phải dùng HS256.";
                return false;
            }

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var iss = root.TryGetProperty("iss", out var issEl) ? issEl.GetString() : null;
            var typ = root.TryGetProperty("typ", out var typEl) ? typEl.GetString() : null;
            if (!string.Equals(iss, JwtIssuer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(typ, JwtActivationType, StringComparison.OrdinalIgnoreCase))
            {
                error = "JWT không đúng issuer/type kích hoạt.";
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (root.TryGetProperty("nbf", out var nbfEl) && nbfEl.TryGetInt64(out var nbf) && now < nbf)
            {
                error = "Mã chưa đến thời điểm sử dụng.";
                return false;
            }
            if (root.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp) && now > exp)
            {
                error = "Mã đã hết hạn.";
                return false;
            }

            var planStr = root.TryGetProperty("plan", out var planEl) ? planEl.GetString() : "WEEK";
            var norm = (planStr ?? "WEEK").Trim().ToUpperInvariant();
            plan = norm switch
            {
                "MONTH" or "1M" or "30D" => QrActivationPlan.Month,
                _ => QrActivationPlan.Week
            };
            return true;
        }
        catch
        {
            error = "JWT không hợp lệ.";
            return false;
        }
    }

    static string ComputeBase64UrlHmacSha256(string text, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Base64UrlEncode(bytes);
    }

    static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };
        return Convert.FromBase64String(padded);
    }

    static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Nhãn hiển thị (Cài đặt) khớp logic server: <c>…30_days_activation:Mixi</c> → Mixi.</summary>
    public static string ResolveDisplayLabel(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return "30 ngày";
        if (payload.Count(c => c == '.') == 2) return "JWT 7 ngày";
        if (!payload.Contains(ActivationKey30Days, StringComparison.OrdinalIgnoreCase))
            return payload.Contains(RequiredToken, StringComparison.OrdinalIgnoreCase) ? "Kích hoạt tour" : "7 ngày";

        var i = payload.IndexOf(ActivationKey30Days, StringComparison.OrdinalIgnoreCase);
        var afterKey = payload.AsSpan(i + ActivationKey30Days.Length);
        if (afterKey.Length > 0 && afterKey[0] == ':')
        {
            var tail = afterKey[1..].ToString().Trim();
            if (tail.Length > 0)
            {
                var cut = tail.IndexOf(':');
                var segment = (cut >= 0 ? tail[..cut] : tail).Trim();
                if (segment.Length > 0) return segment;
            }
        }

        return "7 ngày";
    }
}
