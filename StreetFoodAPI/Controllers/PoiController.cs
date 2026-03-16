using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper; // PHẢI CÓ DÒNG NÀY ĐỂ HẾT LỖI QueryAsync

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
        using var conn = new NpgsqlConnection(_connStr);
        // Dapper giúp gọi hàm này trực tiếp từ connection
        var sql = "SELECT * FROM pois";
        var list = await conn.QueryAsync(sql);
        return Ok(list);
    }
}