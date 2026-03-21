using Microsoft.EntityFrameworkCore;
using StreetFood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<StreetFoodDBContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();


// AUTO MIGRATION
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<StreetFoodDBContext>();

//    Console.WriteLine("RUNNING SQL MIGRATIONS...");
//    await DbInitializer.InitializeAsync(db);
//}

app.Run();