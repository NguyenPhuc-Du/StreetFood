using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/vendor")]
public class VendorShopController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<VendorShopController> _logger;

    public VendorShopController(IConfiguration config, ILogger<VendorShopController> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
    }

    private Task<int?> GetVendorUserId(string username, string password) =>
        VendorAuthHelper.GetVendorUserId(_connStr, username, password);

    private async Task<bool> VendorOwnsPoi(int vendorUserId, int poiId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        var owns = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int
            FROM restaurant_owners
            WHERE userid = @U
              AND poiid = @P",
            new { U = vendorUserId, P = poiId });

        return owns > 0;
    }

    public record VendorAuthBody(string Username, string Password);

    public record VendorPoiDto(
        int PoiId,
        string PoiName,
        string? Address,
        string? ImageUrl,
        string? OpeningHours,
        string? Phone,
        string? Email,
        string ScriptSubmissionState);

    public record UpdateShopDetailsBody(
        string Username,
        string Password,
        int PoiId,
        string? ImageUrl,
        string? OpeningHours,
        string? Phone,
        string? Email);

    public record ListFoodsBody(string Username, string Password, int PoiId);

    // Use a mutable DTO (not positional record) so Dapper can materialize reliably.
    public class FoodDtoForVendor
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        // DB column Foods.Price is INT, so keep it as int for clean mapping.
        public int Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public record CreateFoodBody(
        string Username,
        string Password,
        int PoiId,
        string Name,
        string? Description,
        decimal Price,
        string? ImageUrl);

    public record UpdateFoodBody(
        string Username,
        string Password,
        int FoodId,
        string Name,
        string? Description,
        decimal Price,
        string? ImageUrl);

    public record DeleteFoodBody(string Username, string Password, int FoodId);

    [HttpPost("pois/list")]
    public async Task<IActionResult> ListVendorPois([FromBody] VendorAuthBody body)
    {
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var rows = await conn.QueryAsync<VendorPoiDto>(@"
                SELECT
                    p.Id AS PoiId,
                    t.Name AS PoiName,
                    t.Description AS Address,
                    p.ImageUrl AS ImageUrl,
                    d.OpeningHours,
                    d.Phone,
                    usr.email AS Email,
                    COALESCE(p.scriptsubmissionstate, 'awaiting_vendor') AS ScriptSubmissionState
                FROM restaurant_owners o
                INNER JOIN POIs p ON o.poiid = p.Id
                INNER JOIN POI_Translations t ON t.PoiId = p.Id AND t.LanguageCode = 'vi'
                LEFT JOIN Restaurant_Details d ON d.PoiId = p.Id
                INNER JOIN users usr ON usr.id = o.userid
                WHERE o.userid = @U
                ORDER BY p.Id DESC",
                new { U = userId.Value });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListVendorPois");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("shop/update-details")]
    public async Task<IActionResult> UpdateShopDetails([FromBody] UpdateShopDetailsBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");
        if (!await VendorOwnsPoi(userId.Value, body.PoiId)) return BadRequest("Bạn không quản lý POI này.");

        if (!VendorFieldValidation.IsValidOpeningHours(body.OpeningHours))
            return BadRequest("Giờ mở cửa phải đúng dạng HH:mm - HH:mm (ví dụ 07:00 - 22:00) và giờ mở phải trước giờ đóng.");
        if (!VendorFieldValidation.IsValidPhone(body.Phone))
            return BadRequest("Số điện thoại không đúng định dạng VN (vd: 0912345678 hoặc +84912345678).");
        if (!VendorFieldValidation.IsValidEmail(body.Email))
            return BadRequest("Email không đúng định dạng.");
        if (!string.IsNullOrWhiteSpace(body.ImageUrl) && body.ImageUrl.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Không lưu ảnh dạng base64. Hãy tải ảnh từ máy lên máy chủ (Chọn ảnh).");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.ExecuteAsync(@"
                UPDATE POIs
                SET ImageUrl = @ImageUrl
                WHERE Id = @PoiId",
                new
                {
                    PoiId = body.PoiId,
                    ImageUrl = body.ImageUrl ?? ""
                });

            var phone = VendorFieldValidation.NormalizePhone(body.Phone);
            var email = (body.Email ?? "").Trim();

            await conn.ExecuteAsync(@"
                UPDATE users
                SET email = @Email
                WHERE id = @UserId",
                new
                {
                    UserId = userId.Value,
                    Email = string.IsNullOrEmpty(email) ? (string?)null : email
                });

            await conn.ExecuteAsync(@"
                INSERT INTO Restaurant_Details (PoiId, OpeningHours, Phone)
                VALUES (@PoiId, @OpeningHours, @Phone)
                ON CONFLICT (poiid) DO UPDATE SET
                    openinghours = EXCLUDED.openinghours,
                    phone = EXCLUDED.phone",
                new
                {
                    PoiId = body.PoiId,
                    OpeningHours = (body.OpeningHours ?? "").Trim(),
                    Phone = phone
                });
            return Ok(new { message = "Đã cập nhật thông tin cửa hàng." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateShopDetails");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("foods/list")]
    public async Task<IActionResult> ListFoods([FromBody] ListFoodsBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");
        if (!await VendorOwnsPoi(userId.Value, body.PoiId)) return BadRequest("Bạn không quản lý POI này.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            IEnumerable<FoodDtoForVendor> rows;
            try
            {
                rows = await conn.QueryAsync<FoodDtoForVendor>(@"
                    SELECT
                        Id,
                        PoiId,
                        Name,
                        Description,
                        Price,
                        ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId
                      AND COALESCE(IsHidden, FALSE) = FALSE
                    ORDER BY Id DESC",
                    new { PoiId = body.PoiId });
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                // Migration drift safety: if IsHidden column doesn't exist yet,
                // still show foods instead of breaking the UI.
                rows = await conn.QueryAsync<FoodDtoForVendor>(@"
                    SELECT
                        Id,
                        PoiId,
                        Name,
                        Description,
                        Price,
                        ImageUrl
                    FROM Foods
                    WHERE PoiId = @PoiId
                    ORDER BY Id DESC",
                    new { PoiId = body.PoiId });
            }
            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListFoods");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("foods/create")]
    public async Task<IActionResult> CreateFood([FromBody] CreateFoodBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        if (body.Price <= 0) return BadRequest("Giá món ăn phải lớn hơn 0.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");
        if (!await VendorOwnsPoi(userId.Value, body.PoiId)) return BadRequest("Bạn không quản lý POI này.");
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Cần tên món ăn.");
        if (!string.IsNullOrWhiteSpace(body.ImageUrl) && body.ImageUrl.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Không gửi ảnh base64. Hãy chọn ảnh từ máy để tải lên.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Foods (PoiId, Name, Description, Price, ImageUrl)
                VALUES (@PoiId, @Name, @Description, CAST(@Price AS INT), @ImageUrl)
                RETURNING Id",
                new
                {
                    PoiId = body.PoiId,
                    Name = body.Name.Trim(),
                    Description = body.Description ?? "",
                    Price = body.Price,
                    ImageUrl = body.ImageUrl ?? ""
                });

            return Ok(new { message = "Đã tạo món ăn.", foodId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateFood");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("foods/update")]
    public async Task<IActionResult> UpdateFood([FromBody] UpdateFoodBody body)
    {
        if (body.FoodId <= 0) return BadRequest("FoodId không hợp lệ.");
        if (body.Price <= 0) return BadRequest("Giá món ăn phải lớn hơn 0.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Cần tên món ăn.");
        if (!string.IsNullOrWhiteSpace(body.ImageUrl) && body.ImageUrl.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Không gửi ảnh base64. Hãy chọn ảnh từ máy để tải lên.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var poiId = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT PoiId FROM Foods WHERE Id = @Id",
                new { Id = body.FoodId });

            if (poiId == null) return NotFound("Không tìm thấy món ăn.");
            if (!await VendorOwnsPoi(userId.Value, poiId.Value)) return BadRequest("Bạn không quản lý POI này.");

            var n = await conn.ExecuteAsync(@"
                UPDATE Foods
                SET Name = @Name,
                    Description = @Description,
                    Price = CAST(@Price AS INT),
                    ImageUrl = @ImageUrl
                WHERE Id = @Id",
                new
                {
                    Id = body.FoodId,
                    Name = body.Name.Trim(),
                    Description = body.Description ?? "",
                    Price = body.Price,
                    ImageUrl = body.ImageUrl ?? ""
                });

            if (n == 0) return NotFound("Không tìm thấy món ăn.");
            return Ok(new { message = "Đã cập nhật món ăn." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateFood");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("foods/delete")]
    public async Task<IActionResult> DeleteFood([FromBody] DeleteFoodBody body)
    {
        if (body.FoodId <= 0) return BadRequest("FoodId không hợp lệ.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            var poiId = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT PoiId FROM Foods WHERE Id = @Id",
                new { Id = body.FoodId });

            if (poiId == null) return NotFound("Không tìm thấy món ăn.");
            if (!await VendorOwnsPoi(userId.Value, poiId.Value)) return BadRequest("Bạn không quản lý POI này.");

            try
            {
                // Soft delete: mark IsHidden=true instead of deleting row.
                await conn.ExecuteAsync(@"
                    UPDATE Foods
                    SET IsHidden = TRUE
                    WHERE Id = @Id",
                    new { Id = body.FoodId });
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                return BadRequest("Chưa có cột IsHidden trong bảng Foods. Vui lòng chạy migration V12__Foods_soft_delete.sql.");
            }

            return Ok(new { message = "Đã ẩn món ăn." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteFood");
            return BadRequest(ex.Message);
        }
    }
}

