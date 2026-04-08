using Microsoft.EntityFrameworkCore;
using StreetFood.API.Services;
using StreetFood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ SERVICES (DI Container) ---

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5_242_880;
});

builder.Services.AddHttpClient<AzureTranslatorClient>();
builder.Services.AddScoped<AzureSpeechTtsService>();
builder.Services.AddScoped<R2StorageService>();

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

// CORS: development cho phép mọi localhost (tránh lệch cổng HTTPS/HTTP)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                    origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)
                    || origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase))
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(
                    "http://localhost:5191",
                    "http://localhost:5238",
                    "http://localhost:5240",
                    "https://localhost:7238",
                    "https://localhost:7240",
                    "http://localhost:5288",
                    "https://localhost:7288")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// --- 2. CẤU HÌNH PIPELINE (Middleware) ---

// Kích hoạt giao diện Swagger khi chạy ở môi trường Development


// App MAUI gọi http://IP-LAN:5191 — redirect HTTPS thường đẩy sang cổng 443 và làm lỗi kết nối trên điện thoại.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// THỨ TỰ QUAN TRỌNG: Cors -> Session -> Auth
app.UseCors();
app.UseSession();

app.UseAuthorization();

app.MapControllers();

// Kiểm tra nhanh từ trình duyệt điện thoại: http://[IP-PC]:5191/api/health
app.MapGet("/api/health", () => Results.Text("ok", "text/plain"));

//if (app.Configuration.GetValue("Database:ApplySqlScriptsOnStartup", false))
//{
//    using var scope = app.Services.CreateScope();
//    var db = scope.ServiceProvider.GetRequiredService<StreetFoodDBContext>();
//    await DbInitializer.InitializeAsync(db);
//    Console.WriteLine("[StreetFood] Đã xử lý migration SQL (chỉ chạy file mới).");
//}

await app.RunAsync();