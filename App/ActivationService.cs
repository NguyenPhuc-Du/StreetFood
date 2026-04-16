using System.Globalization;
using Microsoft.Maui.Storage;

namespace App;

public static class ActivationService
{
    const string ExpiryUtcKey = "activation_expires_utc";
    const string PlanKey = "activation_plan";
    const string InstallIdKey = "device_install_id";

    public static bool IsCurrentlyActivated()
    {
        var s = Preferences.Default.Get(ExpiryUtcKey, string.Empty);
        if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var exp))
            return false;
        return DateTime.UtcNow < exp;
    }

    static void ApplyServerUtc(DateTime expiresUtc, string? planLabel)
    {
        Preferences.Default.Set(ExpiryUtcKey, expiresUtc.ToString("o", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(planLabel))
            Preferences.Default.Set(PlanKey, planLabel);
        else
            Preferences.Default.Remove(PlanKey);
    }

    /// <summary>Kích hoạt cục bộ sau khi quét QR hợp lệ (Preferences; không phụ thuộc DB).</summary>
    public static void ApplyLocalFromQr(string payload, QrActivationPlan plan)
    {
        var days = plan switch
        {
            QrActivationPlan.Week => 7,
            QrActivationPlan.Month => 30,
            _ => 7
        };
        var exp = DateTime.UtcNow.AddDays(days);
        var label = QrAccess.ResolveDisplayLabel(payload);
        ApplyServerUtc(exp, label);
    }

    /// <summary>Mã cố định mỗi lần cài app; dùng hỗ trợ / đối chiếu khi có API server.</summary>
    public static string GetOrCreateInstallId()
    {
        var id = Preferences.Default.Get(InstallIdKey, string.Empty);
        if (!string.IsNullOrEmpty(id))
            return id;
        id = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(InstallIdKey, id);
        return id;
    }
}

public enum QrActivationPlan
{
    None,
    /// <summary>QR đơn: chỉ cần chứa StreetFood — mặc định 7 ngày (cục bộ).</summary>
    Standard,
    Week,
    Month
}
