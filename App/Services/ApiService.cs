using App.Models;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Maui.Storage;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;

namespace App.Services;

public class ApiService
{
    /// <summary>Một instance dùng chung để tái sử dụng kết nối HTTP và cache.</summary>
    public static ApiService Instance { get; } = new();

    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };

    static string ApiPoiUrl => $"{ApiConfig.GetBaseUrl()}/api/poi";
    private const string LanguageKey = "appLanguage";
    private static readonly LocalCacheService _cache = new();
    private static bool _cacheInitialized;
    private static readonly ConcurrentDictionary<string, List<Poi>> _poisMemory = new();
    private static readonly ConcurrentDictionary<string, PoiDetail> _detailMemory = new();
    private static readonly ConcurrentDictionary<int, DateTime> _visitCooldown = new();
    private static readonly ConcurrentDictionary<string, DateTime> _locationLogCooldown = new();

    // Tối ưu hóa bộ nhớ: Dùng chung một Options duy nhất
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<Poi>> GetPois(bool forceRefresh = false)
    {
        await EnsureCacheInitializedAsync();
        var preferredLanguage = GetPreferredLanguage();
        if (!forceRefresh && _poisMemory.TryGetValue(preferredLanguage, out var mem) && mem.Count > 0)
            return mem;

        if (IsOnline())
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ApiPoiUrl);
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(preferredLanguage));

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var pois = JsonSerializer.Deserialize<List<Poi>>(json, _options) ?? new List<Poi>();
                    await _cache.SavePoisAsync(preferredLanguage, pois);
                    _poisMemory[preferredLanguage] = pois;
                    return pois;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        }

        if (_poisMemory.TryGetValue(preferredLanguage, out var memoryFallback) && memoryFallback.Count > 0)
            return memoryFallback;

        var cached = await _cache.GetPoisAsync(preferredLanguage);
        _poisMemory[preferredLanguage] = cached;
        return cached;
    }

    public async Task<PoiDetail?> GetPoiDetail(int poiId, bool forceRefresh = false)
    {
        await EnsureCacheInitializedAsync();
        var preferredLanguage = GetPreferredLanguage();
        var key = $"{preferredLanguage}:{poiId}";
        if (!forceRefresh && _detailMemory.TryGetValue(key, out var mem))
            return mem;

        if (IsOnline())
        {
            try
            {
                var url = $"{ApiPoiUrl}/{poiId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(preferredLanguage));

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var detail = JsonSerializer.Deserialize<PoiDetail>(json, _options);
                    if (detail != null)
                    {
                        FillMissingDetailFieldsFromMemory(detail, preferredLanguage);
                        await _cache.SavePoiDetailAsync(preferredLanguage, detail);
                        _detailMemory[key] = detail;
                    }
                    return detail;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        }

        if (_detailMemory.TryGetValue(key, out var memoryFallback))
            return memoryFallback;

        var cached = await _cache.GetPoiDetailAsync(preferredLanguage, poiId);
        if (cached != null)
        {
            FillMissingDetailFieldsFromMemory(cached, preferredLanguage);
            _detailMemory[key] = cached;
        }
        return cached;
    }

    public async Task<List<Poi>> GetTopPois(int top = 10, int days = 30)
    {
        if (!IsOnline()) return await GetPois();
        var preferredLanguage = GetPreferredLanguage();
        try
        {
            var url = $"{ApiPoiUrl}/top?top={Math.Clamp(top, 1, 20)}&days={Math.Clamp(days, 1, 365)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(preferredLanguage));
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return await GetPois();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Poi>>(json, _options) ?? new List<Poi>();
        }
        catch
        {
            return await GetPois();
        }
    }

    public async Task TrackPoiVisitAsync(string deviceId, int poiId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || poiId <= 0 || !IsOnline())
            return;

        var now = DateTime.UtcNow;
        if (_visitCooldown.TryGetValue(poiId, out var last) && (now - last).TotalMinutes < 5)
            return;
        _visitCooldown[poiId] = now;

        try
        {
            await client.PostAsJsonAsync($"{ApiPoiUrl}/visit", new { deviceId, poiId, enteredAtUtc = now });
        }
        catch
        {
            // analytics best-effort, never block UX
        }
    }

    public async Task SendLocationLog(string deviceId, double lat, double lng)
    {
        if (!IsOnline() || string.IsNullOrWhiteSpace(deviceId)) return;
        var key = deviceId.Trim();
        var now = DateTime.UtcNow;
        if (_locationLogCooldown.TryGetValue(key, out var last) && (now - last).TotalSeconds < 12)
            return;
        _locationLogCooldown[key] = now;
        try
        {
            var log = new { DeviceId = deviceId, Latitude = lat, Longitude = lng };
            await client.PostAsJsonAsync($"{ApiPoiUrl}/log", log);
        }
        catch { }
    }

    public async Task StartVisitSessionAsync(string deviceId, int poiId)
    {
        if (!IsOnline() || string.IsNullOrWhiteSpace(deviceId) || poiId <= 0) return;
        try
        {
            await client.PostAsJsonAsync($"{ApiPoiUrl}/visit/start", new { deviceId, poiId, atUtc = DateTime.UtcNow });
        }
        catch { }
    }

    public async Task EndVisitSessionAsync(string deviceId, int poiId)
    {
        if (!IsOnline() || string.IsNullOrWhiteSpace(deviceId) || poiId <= 0) return;
        try
        {
            await client.PostAsJsonAsync($"{ApiPoiUrl}/visit/end", new { deviceId, poiId, atUtc = DateTime.UtcNow });
        }
        catch { }
    }

    public async Task TrackMovementAsync(string deviceId, int fromPoiId, int toPoiId)
    {
        if (!IsOnline() || string.IsNullOrWhiteSpace(deviceId) || fromPoiId <= 0 || toPoiId <= 0 || fromPoiId == toPoiId)
            return;
        try
        {
            await client.PostAsJsonAsync($"{ApiPoiUrl}/movement", new
            {
                deviceId,
                fromPoiId,
                toPoiId,
                atUtc = DateTime.UtcNow
            });
        }
        catch { }
    }

    private static bool IsOnline() => NetworkReachability.HasUsableConnection;

    private static async Task EnsureCacheInitializedAsync()
    {
        if (_cacheInitialized) return;
        await _cache.InitializeAsync();
        _cacheInitialized = true;
    }

    private static string GetPreferredLanguage()
    {
        return Preferences.Default.Get(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    private static void FillMissingDetailFieldsFromMemory(PoiDetail detail, string lang)
    {
        if (!string.IsNullOrWhiteSpace(detail.Address) && !string.IsNullOrWhiteSpace(detail.Description))
            return;
        if (!_poisMemory.TryGetValue(lang, out var list)) return;
        var src = list.FirstOrDefault(p => p.Id == detail.Id);
        if (src == null) return;
        detail.Address ??= src.Address;
        detail.Description ??= src.Description;
        detail.OpeningHours ??= src.OpeningHours;
    }

    public async Task WarmupPoiDetailsAsync(IEnumerable<int> poiIds)
    {
        var ids = poiIds?.Distinct().Where(x => x > 0).Take(5).ToList() ?? new List<int>();
        foreach (var id in ids)
            _ = GetPoiDetail(id);
        await Task.CompletedTask;
    }

    public void ClearInMemoryCache()
    {
        _poisMemory.Clear();
        _detailMemory.Clear();
    }
}