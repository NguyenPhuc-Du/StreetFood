using Microsoft.EntityFrameworkCore;
using StreetFood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ SERVICES (DI Container) ---

builder.Services.AddControllers();

// Cấu hình Database (PostgreSQL)
builder.Services.AddDbContext<StreetFoodDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Swagger/OpenAPI (Để có giao diện test tại /swagger)
builder.Services.AddEndpointsApiExplorer();


// Cấu hình Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cấu hình CORS (Cho phép Frontend gọi API)
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- 2. CẤU HÌNH PIPELINE (Middleware) ---

// Kích hoạt giao diện Swagger khi chạy ở môi trường Development


app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// THỨ TỰ QUAN TRỌNG: Cors -> Session -> Auth
app.UseCors();
app.UseSession();

app.UseAuthorization();

app.MapControllers();

// --- 3. TỰ ĐỘNG CHẠY MIGRATION (Nếu cần) ---
/*
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StreetFoodDBContext>();
    // await db.Database.MigrateAsync(); // Tự động tạo bảng nếu chưa có
}
*/

app.Run();