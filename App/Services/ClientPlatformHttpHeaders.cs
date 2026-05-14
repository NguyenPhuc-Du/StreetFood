using Microsoft.Maui.Devices;

namespace App.Services;

/// <summary>
/// Gửi nền tảng thật (Android / iOS / …) lên API để log phía server khớp với máy chạy app.
/// </summary>
public static class ClientPlatformHttpHeaders
{
    public const string PlatformHeaderName = "X-StreetFood-Client-Platform";

    public static string GetPlatformValue()
    {
        var p = DeviceInfo.Current.Platform;
        if (p == DevicePlatform.Android) return "android";
        if (p == DevicePlatform.iOS) return "ios";
        if (p == DevicePlatform.MacCatalyst) return "maccatalyst";
        if (p == DevicePlatform.WinUI) return "windows";
        if (p == DevicePlatform.Tizen) return "tizen";
        return p.ToString().ToLowerInvariant();
    }

    public static void ApplyTo(HttpClient client)
    {
        string value;
        try
        {
            value = GetPlatformValue();
        }
        catch
        {
            value = "unknown";
        }

        client.DefaultRequestHeaders.Remove(PlatformHeaderName);
        client.DefaultRequestHeaders.TryAddWithoutValidation(PlatformHeaderName, value);
    }
}
