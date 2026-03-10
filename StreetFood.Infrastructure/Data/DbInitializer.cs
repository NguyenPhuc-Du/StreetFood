using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;

namespace StreetFood.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(StreetFoodDBContext db)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        // lấy path folder Migrations
        var basePath = AppContext.BaseDirectory;
        var sqlPath = Path.Combine(basePath, "Migrations");

        Console.WriteLine($"SQL Path: {sqlPath}");

        if (!Directory.Exists(sqlPath))
        {
            Console.WriteLine("Migrations folder not found!");
            return;
        }

        var files = Directory.GetFiles(sqlPath, "*.sql")
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            Console.WriteLine($"Running: {fileName}");

            var sql = await File.ReadAllTextAsync(file);

            using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}