using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StreetFood.Infrastructure.Data;

public static class DbInitializer
{
    private static readonly Regex MigrationVersion = new(@"^V(\d+)__", RegexOptions.Compiled);

    /// <summary>
    /// Số sau V (V1, V2, … V11) — không dùng OrderBy chuỗi (V10 chạy trước V2).
    /// </summary>
    private static int MigrationOrderKey(string filePath)
    {
        var name = Path.GetFileName(filePath);
        var m = MigrationVersion.Match(name);
        return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue;
    }

    public static async Task InitializeAsync(StreetFoodDBContext db)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var basePath = AppContext.BaseDirectory;
        var sqlPath = Path.Combine(basePath, "Migrations");

        Console.WriteLine($"[DbInitializer] SQL Path: {sqlPath}");

        if (!Directory.Exists(sqlPath))
        {
            Console.WriteLine("[DbInitializer] Thư mục Migrations không tìm thấy (kiểm tra file .sql được copy vào output API).");
            return;
        }

        var files = Directory.GetFiles(sqlPath, "*.sql")
            .OrderBy(MigrationOrderKey)
            .ThenBy(Path.GetFileName)
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