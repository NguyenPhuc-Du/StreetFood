using System.Text.Json;
using Microsoft.Maui.Storage;

namespace App.Services;

public static class ApiConfig
{
    public const string PrefKey = "streetfood_api_base_url";
    public const string EnvBaseUrlKey = "STREETFOOD_API_BASE_URL";
    const string EmbeddedDevSettingsUser = "App.appsettings.Development.json";
    const string EmbeddedDevSettingsExample = "App.appsettings.Development.example.json";

    static readonly object DevLoadLock = new();
    static string? _devFileBaseUrl;
    static bool _triedLoadDev;

    /// <summary>
    /// URL mặc định khi người dùng chưa lưu gì trong Cài đặt (Preferences).
    /// Điện thoại thật: nên mở Cài đặt và nhập URL API (LAN, hoặc <c>https://…ngrok-free.app</c> khi tunnel).
    /// Emulator Android: thường <c>http://10.0.2.2:5191</c> trỏ tới máy host.
    /// Có thể ghi <c>StreetFood:Api:BaseUrl</c> trong <c>appsettings.Development.json</c> (Debug).
    /// </summary>
    public const string EmbeddedDefaultBaseUrl = "https://contort-bust-golf.ngrok-free.dev";

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

        // 2) appsettings.Development.json / example embedded at build.
        if (TryGetDevFileBaseUrl(out var fromFile))
        {
            return fromFile;
        }

        // 3) Cleanup stale value from old Settings UI.
        var v = Preferences.Default.Get(PrefKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(v))
        {
            Preferences.Default.Remove(PrefKey);
        }

        // 4) Fallback to embedded default.
        return EmbeddedDefaultBaseUrl.TrimEnd('/');
    }

    static bool TryGetDevFileBaseUrl(out string baseUrl)
    {
        baseUrl = string.Empty;
        lock (DevLoadLock)
        {
            if (_triedLoadDev)
            {
                if (string.IsNullOrEmpty(_devFileBaseUrl))
                {
                    return false;
                }

                baseUrl = _devFileBaseUrl;
                return true;
            }

            _triedLoadDev = true;
            var assembly = typeof(ApiConfig).Assembly;
            foreach (var resName in new[] { EmbeddedDevSettingsUser, EmbeddedDevSettingsExample })
            {
                using var stream = assembly.GetManifestResourceStream(resName);
                if (stream is null)
                {
                    continue;
                }

                string? b = null;
                try
                {
                    using var doc = JsonDocument.Parse(stream);
                    if (doc.RootElement.TryGetProperty("StreetFood", out var st)
                        && st.TryGetProperty("Api", out var api)
                        && api.TryGetProperty("BaseUrl", out var url))
                    {
                        b = url.GetString();
                    }
                }
                catch
                {
                    // bỏ qua file JSON lỗi; dùng fallback
                }

                if (!string.IsNullOrWhiteSpace(b) && IsHttpUrl(b))
                {
                    _devFileBaseUrl = b.Trim().TrimEnd('/');
                }

                break; // đã tìm được tài nguyên nhúng
            }
        }

        if (string.IsNullOrEmpty(_devFileBaseUrl))
        {
            return false;
        }

        baseUrl = _devFileBaseUrl;
        return true;
    }

    static bool IsHttpUrl(string s) =>
        Uri.TryCreate(s.Trim(), UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

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
