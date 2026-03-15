using System.Text.Json;
using App.Models;

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
}