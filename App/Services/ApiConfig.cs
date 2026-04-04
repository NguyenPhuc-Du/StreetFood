using Microsoft.Maui.Storage;

namespace App.Services;

public static class ApiConfig
{
    public const string PrefKey = "streetfood_api_base_url";

    /// <summary>Cổng trùng launchSettings profile http. Đổi IP theo máy chạy API (Cài đặt trong app).</summary>
    public const string EmbeddedDefaultBaseUrl = "http://192.168.1.11:5191";

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
