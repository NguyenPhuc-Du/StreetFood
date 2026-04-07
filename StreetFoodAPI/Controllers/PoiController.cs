using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using StreetFood.API.Models;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly string _connStr;

    public PoiController(IConfiguration config)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
    }

    [HttpGet]
    public async Task<IActionResult> GetPois()
    {
        try
        {
            var acceptLang = Request.Headers["Accept-Language"].ToString();
            string lang = string.IsNullOrEmpty(acceptLang) ? "vi" : acceptLang.Split(',')[0].Trim().Substring(0, 2).ToLower();

            using var conn = new NpgsqlConnection(_connStr);

            // SQL cập nhật: Lấy thêm OpeningHours từ Restaurant_Details
            // Sửa lại SQL trong hàm GetPois
            var sql = @"
            SELECT 
                p.Id, p.Latitude, p.Longitude, COALESCE(p.Radius, 50) AS Radius, p.ImageUrl, 
                t.Name, t.Description as Address,
                d.OpeningHours,
                a.AudioUrl
            FROM POIs p
            INNER JOIN POI_Translations t ON p.Id = t.PoiId
            LEFT JOIN Restaurant_Details d ON p.Id = d.PoiId
            LEFT JOIN Restaurant_Audio a ON p.Id = a.PoiId AND a.LanguageCode = @Lang
            WHERE t.LanguageCode = @Lang";

            var list = (await conn.QueryAsync<PoiDto>(sql, new { Lang = lang })).ToList();

            if (!list.Any() && lang != "vi")
                list = (await conn.QueryAsync<PoiDto>(sql, new { Lang = "vi" })).ToList();

            return Ok(list);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPoiDetail([FromRoute] int id)
    {
        try
        {
            var acceptLang = Request.Headers["Accept-Language"].ToString();
            string lang = string.IsNullOrEmpty(acceptLang) ? "vi" : acceptLang.Split(',')[0].Trim().Substring(0, 2).ToLower();

            using var conn = new NpgsqlConnection(_connStr);

            var detailSql = @"
                SELECT 
                    p.Id, p.Latitude, p.Longitude, p.ImageUrl,
                    t.Name, t.Description as Address,
                    d.OpeningHours, d.Phone,
                    a.AudioUrl
                FROM POIs p
                INNER JOIN POI_Translations t ON p.Id = t.PoiId
                LEFT JOIN Restaurant_Details d ON p.Id = d.PoiId
                LEFT JOIN Restaurant_Audio a ON p.Id = a.PoiId AND a.LanguageCode = @Lang
                WHERE p.Id = @PoiId AND t.LanguageCode = @Lang";

            var detail = (await conn.QueryAsync<PoiDetailDto>(detailSql, new { PoiId = id, Lang = lang })).FirstOrDefault();

            // fallback to Vietnamese when translation missing
            if (detail == null && lang != "vi")
            {
                detail = (await conn.QueryAsync<PoiDetailDto>(detailSql, new { PoiId = id, Lang = "vi" })).FirstOrDefault();
            }

            if (detail == null) return NotFound();

            List<FoodDto> foods;
            try
            {
                var foodsSql = @"
                    SELECT Id, PoiId, Name, Description, Price, ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId
                      AND COALESCE(IsHidden, FALSE) = FALSE";

                foods = (await conn.QueryAsync<FoodDto>(foodsSql, new { PoiId = id })).ToList();
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                // Migration drift safety: column missing => fallback to showing all foods.
                var foodsSql = @"
                    SELECT Id, PoiId, Name, Description, Price, ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId";

                foods = (await conn.QueryAsync<FoodDto>(foodsSql, new { PoiId = id })).ToList();
            }
            detail.Foods = foods;

            return Ok(detail);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}