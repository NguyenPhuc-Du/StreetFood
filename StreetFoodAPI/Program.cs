using Microsoft.EntityFrameworkCore;
using Prometheus;
using StreetFood.API.Middleware;
using StreetFood.API.Services;
using StreetFood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5_242_880;
});

builder.Services.AddHttpClient<AzureTranslatorClient>();
builder.Services.AddScoped<AzureSpeechTtsService>();
builder.Services.AddScoped<R2StorageService>();
builder.Services.AddSingleton<PremiumService>();
builder.Services.AddSingleton<PoiIngressQueueService>();
builder.Services.AddSingleton<ListenEventQueueService>();
builder.Services.AddHostedService<ListenEventQueueWorker>();

builder.Services.AddDbContext<StreetFoodDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();



builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();
app.UseSession();

app.UseHttpMetrics();
app.UseMiddleware<RequestIdLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapMetrics("/api/metrics");

app.MapGet("/api/health", () => Results.Text("ok", "text/plain"));
app.MapGet("/", (HttpContext context) =>
{
    if (context.Request.Query.ContainsKey("partnerCode")
        || context.Request.Query.ContainsKey("orderId")
        || context.Request.Query.ContainsKey("resultCode"))
    {
        var qs = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";
        return Results.Redirect($"/payment/return{qs}", permanent: false);
    }

    return Results.NotFound();
});
app.MapGet("/payment/return", (HttpContext context, IConfiguration config) =>
{
    var vendorHome = (config["Momo:VendorHomepageUrl"] ?? "https://localhost:7240/html/dashboardShopPage.html").Trim();
    if (!Uri.TryCreate(vendorHome, UriKind.Absolute, out var target)
        || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
    {
        vendorHome = "https://localhost:7240/html/dashboardShopPage.html";
    }

    var qs = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";
    return Results.Redirect($"{vendorHome}{qs}", permanent: false);
});

//if (app.Configuration.GetValue("Database:ApplySqlScriptsOnStartup", false))
//{
//    using var scope = app.Services.CreateScope();
//    var db = scope.ServiceProvider.GetRequiredService<StreetFoodDBContext>();
//    await DbInitializer.InitializeAsync(db);
//}

await app.RunAsync();