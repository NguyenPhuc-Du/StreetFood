using Microsoft.AspNetCore.Mvc;
using StreetFood.API.Models;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // Giả sử bạn dùng DB Context để check
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // 1. Tìm user trong DB (bảng users bạn gửi lúc nãy)
        // var user = _context.Users.FirstOrDefault(u => u.Username == request.Username && u.Password == request.Password);

        // Demo kiểm tra nhanh:
        if (request.Username == "highlands_owner" && request.Password == "vendor123")
        {
            return Ok(new { role = "vendor", message = "Login success" });
        }

        return Unauthorized("Sai tài khoản hoặc mật khẩu");
    }
}