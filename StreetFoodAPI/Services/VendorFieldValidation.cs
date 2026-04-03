using System.Text.RegularExpressions;

namespace StreetFood.API.Services;

public static class VendorFieldValidation
{
    /// <summary>VN mobile: 0xxxxxxxxx hoặc +84xxxxxxxxx (9 số sau mã vùng).</summary>
    private static readonly Regex PhoneRegex = new(
        @"^(?:\+84|0)(3|5|7|8|9)[0-9]{8}$",
        RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex OpeningHoursRegex = new(
        @"^\s*(\d{2}:\d{2})\s*-\s*(\d{2}:\d{2})\s*$",
        RegexOptions.Compiled);

    public static bool IsValidPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var s = raw.Trim().Replace(" ", "").Replace("-", "");
        return PhoneRegex.IsMatch(s);
    }

    public static bool IsValidEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var s = raw.Trim();
        return s.Length <= 255 && EmailRegex.IsMatch(s);
    }

    /// <summary>Dạng "HH:mm - HH:mm", giờ mở &lt; giờ đóng (cùng ngày).</summary>
    public static bool IsValidOpeningHours(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var m = OpeningHoursRegex.Match(raw.Trim());
        if (!m.Success) return false;
        if (!TryParseHm(m.Groups[1].Value, out var a) || !TryParseHm(m.Groups[2].Value, out var b))
            return false;
        return a < b;
    }

    private static bool TryParseHm(string hm, out int minutes)
    {
        minutes = 0;
        var parts = hm.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var min)) return false;
        if (h is < 0 or > 23 || min is < 0 or > 59) return false;
        minutes = h * 60 + min;
        return true;
    }

    public static string NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().Replace(" ", "").Replace("-", "");
    }
}
