using Microsoft.EntityFrameworkCore;
using Prometheus;
using StreetFood.API.Middleware;
using StreetFood.API.Services;
using StreetFood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ÄÄ‚NG KÃ SERVICES (DI Container) ---

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5_242_880;
});

builder.Services.AddHttpClient<AzureTranslatorClient>();
builder.Services.AddScoped<AzureSpeechTtsService>();
builder.Services.AddScoped<R2StorageService>();
builder.Services.Configure<AudioPipelineOptions>(builder.Configuration.GetSection(AudioPipelineOptions.SectionName));
builder.Services.AddSingleton<AudioPipelineJobStore>();
builder.Services.AddHostedService<AudioPipelineJobWorker>();
builder.Services.AddSingleton<PoiIngressQueueService>();

// Cáº¥u hÃ¬nh Database (PostgreSQL)
builder.Services.AddDbContext<StreetFoodDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cáº¥u hÃ¬nh Swagger/OpenAPI (Äá»ƒ cÃ³ giao diá»‡n test táº¡i /swagger)
builder.Services.AddEndpointsApiExplorer();


// Cáº¥u hÃ¬nh Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// CORS: development cho phÃ©p má»i localhost (trÃ¡nh lá»‡ch cá»•ng HTTPS/HTTP)
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

// --- 2. Cáº¤U HÃŒNH PIPELINE (Middleware) ---

// KÃ­ch hoáº¡t giao diá»‡n Swagger khi cháº¡y á»Ÿ mÃ´i trÆ°á»ng Development


// App MAUI gá»i http://IP-LAN:5191 â€” redirect HTTPS thÆ°á»ng Ä‘áº©y sang cá»•ng 443 vÃ  lÃ m lá»—i káº¿t ná»‘i trÃªn Ä‘iá»‡n thoáº¡i.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// THá»¨ Tá»° QUAN TRá»ŒNG: Cors -> Session -> Auth
app.UseCors();
app.UseSession();

app.UseHttpMetrics();
app.UseMiddleware<RequestIdLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapMetrics("/api/metrics");

// Kiá»ƒm tra nhanh tá»« trÃ¬nh duyá»‡t Ä‘iá»‡n thoáº¡i: http://[IP-PC]:5191/api/health
app.MapGet("/api/health", () => Results.Text("ok", "text/plain"));

//if (app.Configuration.GetValue("Database:ApplySqlScriptsOnStartup", false))
//{
//    using var scope = app.Services.CreateScope();
//    var db = scope.ServiceProvider.GetRequiredService<StreetFoodDBContext>();
//    await DbInitializer.InitializeAsync(db);
//    Console.WriteLine("[StreetFood] ÄÃ£ xá»­ lÃ½ migration SQL (chá»‰ cháº¡y file má»›i).");
//}

await app.RunAsync();