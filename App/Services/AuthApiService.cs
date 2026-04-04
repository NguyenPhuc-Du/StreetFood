using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Services;

public class AuthApiService
{
    public static AuthApiService Instance { get; } = new();

    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(45) };

    /// <summary>Kích hoạt theo install_id — không đăng nhập; server lưu thiết bị. Tuple: TransientNetworkFailure = không tới được máy chủ (app có thể kích hoạt cục bộ).</summary>
    public async Task<(bool Ok, string? Error, ActivateResponse? Data, bool TransientNetworkFailure)> ActivateDeviceAsync(string installId, string code)
    {
        if (!NetworkReachability.HasUsableConnection)
            return (false, null, null, true);
        try
        {
            var url = $"{ApiConfig.GetBaseUrl()}/api/auth/activate-device";
            var res = await _client.PostAsJsonAsync(url, new { installId, activationCode = code });
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return (false, string.IsNullOrWhiteSpace(json) ? res.ReasonPhrase : json.Trim('"'), null, false);

            var data = JsonSerializer.Deserialize<ActivateResponse>(json, JsonOpts);
            if (data == null) return (false, "Phản hồi không hợp lệ.", null, false);
            return (true, null, data, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AuthApi] activate-device: {ex}");
            if (ex is HttpRequestException or TaskCanceledException)
                return (false, "Không kết nối được máy chủ.", null, true);

            return (false, ex.Message, null, false);
        }
    }

    public async Task<(bool Ok, DeviceStatusResponse? Data)> GetDeviceStatusAsync(string installId)
    {
        if (!NetworkReachability.HasUsableConnection) return (false, null);
        try
        {
            var url = $"{ApiConfig.GetBaseUrl()}/api/auth/device-status?installId={Uri.EscapeDataString(installId)}";
            var res = await _client.GetAsync(url);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return (false, null);
            var data = JsonSerializer.Deserialize<DeviceStatusResponse>(json, JsonOpts);
            return (true, data);
        }
        catch
        {
            return (false, null);
        }
    }
}

public sealed class ActivateResponse
{
    [JsonPropertyName("activationExpiresAt")]
    public string? ActivationExpiresAt { get; set; }

    [JsonPropertyName("planLabel")]
    public string? PlanLabel { get; set; }
}

public sealed class DeviceStatusResponse
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("activationExpiresAt")]
    public string? ActivationExpiresAt { get; set; }

    [JsonPropertyName("planLabel")]
    public string? PlanLabel { get; set; }
}
