using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StreetFood.API.Services;

namespace StreetFood.API.Controllers;

[ApiController]
[Route("api/vendor")]
public class VendorShopController : ControllerBase
{
    private readonly string _connStr;
    private readonly IConfiguration _config;
    private readonly ILogger<VendorShopController> _logger;
    private readonly PremiumService _premium;
    private readonly IHttpClientFactory _httpClientFactory;

    public VendorShopController(
        IConfiguration config,
        ILogger<VendorShopController> logger,
        PremiumService premium,
        IHttpClientFactory httpClientFactory)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _config = config;
        _logger = logger;
        _premium = premium;
        _httpClientFactory = httpClientFactory;
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
        string ScriptSubmissionState,
        bool IsPremium,
        DateTime? PremiumEndsAtUtc);

    public record PremiumStatusBody(string Username, string Password, int PoiId);
    public record CreateMomoPaymentBody(string Username, string Password, int PoiId, string? ReturnUrl);

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
        public bool IsHidden { get; set; }
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
    public record RestoreFoodBody(string Username, string Password, int FoodId);

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
                    COALESCE(p.scriptsubmissionstate, 'awaiting_vendor') AS ScriptSubmissionState,
                    (ps.poi_id IS NOT NULL) AS IsPremium,
                    ps.end_at AS PremiumEndsAtUtc
                FROM restaurant_owners o
                INNER JOIN POIs p ON o.poiid = p.Id
                INNER JOIN POI_Translations t ON t.PoiId = p.Id AND t.LanguageCode = 'vi'
                LEFT JOIN Restaurant_Details d ON d.PoiId = p.Id
                INNER JOIN users usr ON usr.id = o.userid
                LEFT JOIN poi_premium_subscriptions ps
                    ON ps.poi_id = p.id
                    AND ps.status = 'active'
                    AND ps.end_at > NOW()
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
                        ImageUrl,
                        COALESCE(IsHidden, FALSE) AS IsHidden
                    FROM Foods
                    WHERE PoiId = @PoiId
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
                        ImageUrl,
                        FALSE AS IsHidden
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

    [HttpPost("foods/restore")]
    public async Task<IActionResult> RestoreFood([FromBody] RestoreFoodBody body)
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
                await conn.ExecuteAsync(@"
                    UPDATE Foods
                    SET IsHidden = FALSE
                    WHERE Id = @Id",
                    new { Id = body.FoodId });
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                return BadRequest("Chưa có cột IsHidden trong bảng Foods. Vui lòng chạy migration V12__Foods_soft_delete.sql.");
            }

            return Ok(new { message = "Đã bật lại món ăn." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RestoreFood");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("premium/status")]
    public async Task<IActionResult> GetPremiumStatus([FromBody] PremiumStatusBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");
        if (!await VendorOwnsPoi(userId.Value, body.PoiId)) return BadRequest("Bạn không quản lý POI này.");

        var st = await _premium.GetPoiPremiumStatusAsync(body.PoiId, HttpContext.RequestAborted);
        return Ok(new
        {
            poiId = body.PoiId,
            isPremium = st.IsPremium,
            currentPlan = st.IsPremium ? "premium" : "thuong",
            premiumPriceVnd = 199000,
            premiumEndsAtUtc = st.EndsAtUtc
        });
    }

    [HttpPost("premium/create-payment")]
    public async Task<IActionResult> CreateMomoPayment([FromBody] CreateMomoPaymentBody body)
    {
        if (body.PoiId <= 0) return BadRequest("POIId không hợp lệ.");
        var userId = await GetVendorUserId(body.Username, body.Password);
        if (userId == null) return Unauthorized("Sai tài khoản hoặc tài khoản đã bị ẩn.");
        if (!await VendorOwnsPoi(userId.Value, body.PoiId)) return BadRequest("Bạn không quản lý POI này.");

        var st = await _premium.GetPoiPremiumStatusAsync(body.PoiId, HttpContext.RequestAborted);
        if (st.IsPremium)
            return Ok(new { alreadyPremium = true, message = "POI này đang là premium.", premiumEndsAtUtc = st.EndsAtUtc });

        var partnerCode = (_config["Momo:PartnerCode"] ?? "").Trim();
        var accessKey = (_config["Momo:AccessKey"] ?? "").Trim();
        var secretKey = (_config["Momo:SecretKey"] ?? "").Trim();
        var endpoint = (_config["Momo:Endpoint"] ?? "https://test-payment.momo.vn/v2/gateway/api/create").Trim();
        var requestType = (_config["Momo:RequestType"] ?? "captureWallet").Trim();
        var amount = _config.GetValue("Momo:PremiumAmountVnd", 199000);
        var publicBaseUrl = (_config["Api:PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            var req = HttpContext?.Request;
            if (req != null && req.Host.HasValue)
                publicBaseUrl = $"{req.Scheme}://{req.Host.Value}".TrimEnd('/');
        }
        var ipnUrl = (_config["Momo:IpnUrl"] ?? "").Trim();
        var defaultRedirect = (_config["Momo:VendorReturnUrl"] ?? "").Trim();

        if (string.IsNullOrWhiteSpace(ipnUrl) && !string.IsNullOrWhiteSpace(publicBaseUrl))
            ipnUrl = $"{publicBaseUrl}/api/vendor/premium/momo-ipn";
        if (string.IsNullOrWhiteSpace(defaultRedirect) && !string.IsNullOrWhiteSpace(publicBaseUrl))
            defaultRedirect = $"{publicBaseUrl}/html/upgradePage.html";

        // Always use server-side configured return URL for MoMo
        // to avoid stale frontend values overriding redirect behavior.
        var redirectUrl = defaultRedirect;

        if (string.IsNullOrWhiteSpace(partnerCode) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            return BadRequest("Thiếu cấu hình Momo (PartnerCode/AccessKey/SecretKey).");
        if (string.IsNullOrWhiteSpace(ipnUrl) || string.IsNullOrWhiteSpace(redirectUrl))
            return BadRequest("Thiếu Momo:IpnUrl hoặc Momo:VendorReturnUrl (hoặc Api:PublicBaseUrl).");

        var orderId = $"POI{body.PoiId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var requestId = Guid.NewGuid().ToString("N");
        var orderInfo = $"Nang cap Premium POI {body.PoiId}";
        var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes($"poiId={body.PoiId}&vendorUserId={userId.Value}"));

        var rawSign = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";
        var signature = HmacSha256(secretKey, rawSign);

        var reqBody = new
        {
            partnerCode,
            requestId,
            amount = amount.ToString(),
            orderId,
            orderInfo,
            redirectUrl,
            ipnUrl,
            requestType,
            extraData,
            lang = "vi",
            signature
        };

        var reqJson = JsonSerializer.Serialize(reqBody);
        var http = _httpClientFactory.CreateClient();
        var content = new StringContent(reqJson, Encoding.UTF8, "application/json");
        var res = await http.PostAsync(endpoint, content, HttpContext.RequestAborted);
        var txt = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Momo create-payment failed: {Status} {Body}", (int)res.StatusCode, txt);
            return BadRequest("Không tạo được phiên thanh toán MoMo.");
        }

        using var doc = JsonDocument.Parse(txt);
        var root = doc.RootElement;
        var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
        var payUrl = root.TryGetProperty("payUrl", out var pu) ? pu.GetString() : null;
        var deeplink = root.TryGetProperty("deeplink", out var dl) ? dl.GetString() : null;
        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        if (resultCode != 0 || string.IsNullOrWhiteSpace(payUrl))
            return BadRequest($"MoMo trả lỗi: {message ?? "Không lấy được payUrl"}");

        await _premium.UpsertPaymentOrderAsync(
            orderId,
            requestId,
            userId.Value,
            body.PoiId,
            amount,
            "pending",
            txt,
            HttpContext.RequestAborted);

        return Ok(new
        {
            orderId,
            requestId,
            amountVnd = amount,
            payUrl,
            deeplink
        });
    }

    [HttpPost("premium/momo-ipn")]
    public async Task<IActionResult> MomoIpn([FromBody] JsonElement ipn)
    {
        var secretKey = (_config["Momo:SecretKey"] ?? "").Trim();
        var accessKey = (_config["Momo:AccessKey"] ?? "").Trim();
        var orderId = ipn.TryGetProperty("orderId", out var o) ? o.GetString() : null;
        var requestId = ipn.TryGetProperty("requestId", out var r) ? r.GetString() : null;
        var resultCode = ipn.TryGetProperty("resultCode", out var rc) ? ParseInt(rc, -1) : -1;
        var amount = ipn.TryGetProperty("amount", out var am) ? ParseInt(am, _config.GetValue("Momo:PremiumAmountVnd", 199000)) : _config.GetValue("Momo:PremiumAmountVnd", 199000);
        var transId = ipn.TryGetProperty("transId", out var tid) ? tid.GetRawText().Trim('"') : "";
        var extraData = ipn.TryGetProperty("extraData", out var ex) ? (ex.GetString() ?? "") : "";
        var message = ipn.TryGetProperty("message", out var m) ? (m.GetString() ?? "") : "";
        var orderInfo = ipn.TryGetProperty("orderInfo", out var oi) ? (oi.GetString() ?? "") : "";
        var orderType = ipn.TryGetProperty("orderType", out var ot) ? (ot.GetString() ?? "") : "";
        var partnerCode = ipn.TryGetProperty("partnerCode", out var pc) ? (pc.GetString() ?? "") : "";
        var payType = ipn.TryGetProperty("payType", out var pt) ? (pt.GetString() ?? "") : "";
        var responseTime = ipn.TryGetProperty("responseTime", out var rt) ? rt.GetRawText().Trim('"') : "";
        var signature = ipn.TryGetProperty("signature", out var sig) ? (sig.GetString() ?? "") : "";

        if (string.IsNullOrWhiteSpace(orderId))
            return Ok(new { resultCode = 0, message = "ignore" });

        if (!string.IsNullOrWhiteSpace(secretKey)
            && !string.IsNullOrWhiteSpace(signature)
            && !string.IsNullOrWhiteSpace(accessKey))
        {
            var raw = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&message={message}&orderId={orderId}&orderInfo={orderInfo}&orderType={orderType}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={responseTime}&resultCode={resultCode}&transId={transId}";
            var expected = HmacSha256(secretKey, raw);
            if (!string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Momo IPN signature mismatch for order {OrderId}", orderId);
                return Ok(new { resultCode = 0, message = "invalid_signature" });
            }
        }

        var rawJson = ipn.GetRawText();
        if (resultCode == 0)
        {
            await _premium.MarkOrderPaidAndActivateAsync(
                orderId,
                transId,
                rawJson,
                premiumDays: 30,
                amountVnd: amount,
                cancellationToken: HttpContext.RequestAborted);
            return Ok(new { resultCode = 0, message = "ok" });
        }

        await _premium.MarkOrderStatusAsync(
            orderId,
            status: "failed",
            rawResponseJson: rawJson,
            cancellationToken: HttpContext.RequestAborted);

        return Ok(new { resultCode = 0, message = "received" });
    }

    private static string HmacSha256(string secretKey, string rawData)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int ParseInt(JsonElement el, int fallback)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
            return s;
        return fallback;
    }
}

