using Microsoft.Maui.Storage;

namespace App.Services;

public static class ApiConfig
{
    public const string PrefKey = "streetfood_api_base_url";
    public const string EnvBaseUrlKey = "STREETFOOD_API_BASE_URL";

    /// <summary>
    /// URL mặc định khi người dùng chưa lưu gì trong Cài đặt (Preferences).
    /// Điện thoại thật: nên mở Cài đặt và nhập URL API (LAN, hoặc <c>https://…ngrok-free.app</c> khi tunnel).
    /// Emulator Android: thường <c>http://10.0.2.2:5191</c> trỏ tới máy host.
    /// </summary>
    public const string EmbeddedDefaultBaseUrl = "https://flatly-creamer-bucket.ngrok-free.dev";

    public static string GetBaseUrl()
    {
        // 1) Prefer process/system env so changing endpoint does not require code edits.
        var env = Environment.GetEnvironmentVariable(EnvBaseUrlKey);
        if (!string.IsNullOrWhiteSpace(env)
            && Uri.TryCreate(env.Trim(), UriKind.Absolute, out var envUri)
            && (envUri.Scheme == Uri.UriSchemeHttp || envUri.Scheme == Uri.UriSchemeHttps))
        {
            return envUri.ToString().TrimEnd('/');
        }

        // 2) Cleanup stale value from old Settings UI.
        var v = Preferences.Default.Get(PrefKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(v))
        {
            Preferences.Default.Remove(PrefKey);
        }

        // 3) Fallback to embedded default.
        return EmbeddedDefaultBaseUrl.TrimEnd('/');
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
