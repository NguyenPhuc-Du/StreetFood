using App.Models;
using System.Text.Json;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Maui.Storage;

namespace App.Services;

public class ApiService
{
    private readonly HttpClient client = new();
    private readonly string apiUrl = "http://192.168.1.11:5246/api/poi";
    private const string LanguageKey = "appLanguage";

    // Tối ưu hóa bộ nhớ: Dùng chung một Options duy nhất
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<Poi>> GetPois()
    {
        try
        {
            string deviceLang = GetPreferredLanguage();
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(deviceLang));

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Poi>>(json, _options) ?? new List<Poi>();
            }
        }
        catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        return new List<Poi>();
    }

    public async Task<PoiDetail?> GetPoiDetail(int poiId)
    {
        try
        {
            string deviceLang = GetPreferredLanguage();
            var url = $"{apiUrl}/{poiId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(deviceLang));

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PoiDetail>(json, _options);
        }
        catch (Exception ex) { Console.WriteLine($"Lỗi API: {ex.Message}"); }
        return null;
    }

    private static string GetPreferredLanguage()
    {
        return Preferences.Default.Get(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }
}