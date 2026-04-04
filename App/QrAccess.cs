namespace App;

/// <summary>
/// Mã QR kích hoạt trong app. Có mạng và API dùng được thì đồng bộ server; không thì vẫn kích hoạt cục bộ theo gói (30 ngày / tuần / tháng). Không mở trình duyệt từ nội dung QR.
/// Chấp nhận một trong các dạng nội dung:
/// <list type="bullet">
/// <item><description>Chuẩn in ấn: <c>StreetFood:30_days_activation:Mixi</c></description></item>
/// <item><description><c>30_days_activation...</c> (ví dụ <c>30_days_activation_key_mixi_2023</c>)</description></item>
/// <item><description><c>StreetFood</c> (mặc định 30 ngày; tùy chọn <c>|WEEK</c> / <c>|MONTH</c>)</description></item>
/// </list>
/// </summary>
public static class QrAccess
{
    public const string RequiredToken = "StreetFood";

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

    /// <summary>Nhãn hiển thị (Cài đặt) khớp logic server: <c>…30_days_activation:Mixi</c> → Mixi.</summary>
    public static string ResolveDisplayLabel(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return "30 ngày";
        if (!payload.Contains(ActivationKey30Days, StringComparison.OrdinalIgnoreCase))
            return payload.Contains(RequiredToken, StringComparison.OrdinalIgnoreCase) ? "Kích hoạt tour" : "30 ngày";

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

        return "30 ngày";
    }
}
