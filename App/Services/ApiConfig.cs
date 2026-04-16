using Microsoft.Maui.Storage;

namespace App.Services;

public static class ApiConfig
{
    public const string PrefKey = "streetfood_api_base_url";

    /// <summary>Mặc định cho Android Emulator → máy host: <c>http://10.0.2.2:5191</c>. Điện thoại thật cùng Wi‑Fi: đổi IP LAN PC trong Cài đặt.</summary>
    public const string EmbeddedDefaultBaseUrl = "http://172.20.10.2:5191";

    public static string GetBaseUrl()
    {
        var v = Preferences.Default.Get(PrefKey, string.Empty);
        if (string.IsNullOrWhiteSpace(v))
            return EmbeddedDefaultBaseUrl.TrimEnd('/');
        return v.Trim().TrimEnd('/');
    }

    public static void SetBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Preferences.Default.Remove(PrefKey);
            return;
        }

        Preferences.Default.Set(PrefKey, url.Trim().TrimEnd('/'));
    }
}
