using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly IConfiguration _config;

    public PoiController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<List<PoiDto>>> GetPois()
    {
        var list = new List<PoiDto>();

        using var conn = new NpgsqlConnection(
            _config.GetConnectionString("DefaultConnection"));

        await conn.OpenAsync();

        string sql = @"
        SELECT
            p.id,
            t.name,
            p.latitude,
            p.longitude,
            p.imageurl,
            a.audiourl
        FROM pois p
        JOIN poi_translations t
            ON p.id = t.poiid
        LEFT JOIN restaurant_audio a
            ON p.id = a.poiid
        WHERE t.languagecode = 'vi'
        ";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new PoiDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Latitude = reader.GetDouble(2),
                Longitude = reader.GetDouble(3),
                ImageUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                AudioUrl = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return Ok(list);
    }
}