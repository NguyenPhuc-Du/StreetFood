using Microsoft.Maui.Storage;

namespace App.Services;

public static class ApiConfig
{
    public const string PrefKey = "streetfood_api_base_url";
    public const string EnvBaseUrlKey = "STREETFOOD_API_BASE_URL";

    /// <summary>
    /// URL mac dinh khi nguoi dung chua luu gi trong Cai dat (Preferences).
    /// Local dev: dung localhost/LAN cua API.
    /// Emulator Android: thuong <c>http://10.0.2.2:5191</c> tro toi may host.
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
