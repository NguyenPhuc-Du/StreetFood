using App.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace App.Services;

public class ApiService
{
    HttpClient client = new();

    string apiUrl = "http://192.168.1.4:5246/api/poi";

    public async Task<List<Poi>> GetPois()
    {
        try
        {
            var json = await client.GetStringAsync(apiUrl);

            return JsonSerializer.Deserialize<List<Poi>>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Poi>();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return new List<Poi>();
        }
    }
    public async Task SendLocationLog(string deviceId, double lat, double lng)
    {
        try
        {
            var log = new { DeviceId = deviceId, Latitude = lat, Longitude = lng };
            // API này sẽ insert vào bảng Location_Logs trong Neon
            await client.PostAsJsonAsync("http://192.168.1.4:5246/api/poi/log", log);
        }
        catch { /* Quá trình tracking chạy ngầm không nên làm treo app */ }
    }
}