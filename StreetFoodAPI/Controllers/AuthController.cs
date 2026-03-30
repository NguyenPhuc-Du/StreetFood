
﻿using Microsoft.AspNetCore.Mvc;
using StreetFood.API.Models;
using StreetFood.Infrastructure.Data;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly StreetFoodDBContext _context;

    public AuthController(StreetFoodDBContext context)
    {
        _context = context;
    }
    // Giả sử bạn dùng DB Context để check
    [HttpPost("Login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Tìm user trong DbSet 'Users'
        var user = _context.Users
    .Select(u => new { u.username, u.password, u.role })
    .FirstOrDefault(u => u.username == request.Username && u.password == request.Password);

        if (user == null)
        {
            return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });
        }

        // Trả về đúng role cho JavaScript (login.js) xử lý chuyển trang
        return Ok(new
        {
            role = user.role,
            username = user.username,
            message = "Login thành công"
        });

    }
}