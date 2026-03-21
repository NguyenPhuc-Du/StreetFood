using App.Models;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Networking;
using System.Net.Http.Json;
using System.Text.Json;

namespace App.Services;

public class ApiService
{
    private readonly HttpClient client = new();
    private readonly string apiUrl = "http://192.168.1.11:5246/api/poi";
    private const string LanguageKey = "appLanguage";
    private static readonly LocalCacheService _cache = new();
    private static bool _cacheInitialized;

    // Tối ưu hóa bộ nhớ: Dùng chung một Options duy nhất
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<Poi>> GetPois()
    {
        await EnsureCacheInitializedAsync();
        var preferredLanguage = GetPreferredLanguage();

        if (IsOnline())
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(preferredLanguage));

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var pois = JsonSerializer.Deserialize<List<Poi>>(json, _options) ?? new List<Poi>();
                    await _cache.SavePoisAsync(preferredLanguage, pois);
                    return pois;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        }

        return await _cache.GetPoisAsync(preferredLanguage);
    }

    public async Task<PoiDetail?> GetPoiDetail(int poiId)
    {
        await EnsureCacheInitializedAsync();
        var preferredLanguage = GetPreferredLanguage();

        if (IsOnline())
        {
            try
            {
                var url = $"{apiUrl}/{poiId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(preferredLanguage));

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var detail = JsonSerializer.Deserialize<PoiDetail>(json, _options);
                    if (detail != null)
                        await _cache.SavePoiDetailAsync(preferredLanguage, detail);
                    return detail;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        }

        return await _cache.GetPoiDetailAsync(preferredLanguage, poiId);
    }

    public async Task SendLocationLog(string deviceId, double lat, double lng)
    {
        if (!IsOnline()) return;
        try
        {
            var log = new { DeviceId = deviceId, Latitude = lat, Longitude = lng };
            await client.PostAsJsonAsync($"{apiUrl}/log", log);
        }
        catch { }
    }

    private static bool IsOnline()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

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
}