using System.Net.Http.Json;

namespace App.Services;

/// <summary>Gửi thời lượng nghe lên API (fire-and-forget, không chặn UI).</summary>
public static class ListenTelemetry
{
    const int MinSeconds = 3;
    const int MaxSeconds = 3600;
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static void ReportFireAndForget(int poiId, int durationSeconds)
    {
        if (poiId <= 0 || durationSeconds < MinSeconds) return;
        var sec = Math.Min(durationSeconds, MaxSeconds);
        _ = ReportAsync(poiId, sec);
    }

    static async Task ReportAsync(int poiId, int durationSeconds)
    {
        if (!NetworkReachability.HasUsableConnection) return;
        try
        {
            var url = $"{ApiConfig.GetBaseUrl()}/api/analytics/poi-audio-listen";
            var deviceId = ActivationService.GetOrCreateInstallId();
            await Client.PostAsJsonAsync(url, new
            {
                poiId,
                durationSeconds,
                deviceId
            });
        }
        catch
        {
            /* bỏ qua — analytics không được làm hỏng trải nghiệm */
        }
    }
}
