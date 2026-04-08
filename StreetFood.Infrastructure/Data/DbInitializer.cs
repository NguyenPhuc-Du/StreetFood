using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StreetFood.Infrastructure.Data;

public static class DbInitializer
{
    private static readonly Regex MigrationVersion = new(@"^V(\d+)__", RegexOptions.Compiled);
    private const string MigrationTable = "schema_migrations";

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

        await EnsureMigrationTableAsync(connection);
        var applied = await GetAppliedMigrationsAsync(connection);

        // Existing DB bootstrap:
        // If schema already exists but migration history is empty,
        // mark current files as applied to avoid re-running V1..Vn.
        if (applied.Count == 0 && await IsLikelyInitializedDatabaseAsync(connection))
        {
            Console.WriteLine("[DbInitializer] Detected existing schema with empty migration history. Baselining current migration files...");
            await BaselineMigrationsAsync(connection, files);
            applied = await GetAppliedMigrationsAsync(connection);
        }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (applied.Contains(fileName))
            {
                Console.WriteLine($"Skip (already applied): {fileName}");
                continue;
            }

            Console.WriteLine($"Running: {fileName}");

            var sql = await File.ReadAllTextAsync(file);
            var checksum = ComputeSha256(sql);

            await using var tx = await connection.BeginTransactionAsync();
            try
            {
                await using (var cmd = new NpgsqlCommand(sql, connection, tx))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                await using (var recordCmd = new NpgsqlCommand($@"
                    INSERT INTO {MigrationTable} (filename, checksum, appliedat)
                    VALUES (@filename, @checksum, NOW())
                    ON CONFLICT (filename)
                    DO UPDATE SET
                        checksum = EXCLUDED.checksum,
                        appliedat = NOW()", connection, tx))
                {
                    recordCmd.Parameters.AddWithValue("filename", fileName);
                    recordCmd.Parameters.AddWithValue("checksum", checksum);
                    await recordCmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                Console.WriteLine($"Applied: {fileName}");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }

    private static async Task EnsureMigrationTableAsync(NpgsqlConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id SERIAL PRIMARY KEY,
                filename VARCHAR(255) UNIQUE NOT NULL,
                checksum VARCHAR(64) NOT NULL,
                appliedat TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetAppliedMigrationsAsync(NpgsqlConnection connection)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand($"SELECT filename FROM {MigrationTable};", connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    private static async Task<bool> IsLikelyInitializedDatabaseAsync(NpgsqlConnection connection)
    {
        const string sql = @"
            SELECT
                to_regclass('public.users') IS NOT NULL
                AND to_regclass('public.pois') IS NOT NULL
                AND to_regclass('public.foods') IS NOT NULL;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    private static async Task BaselineMigrationsAsync(NpgsqlConnection connection, IEnumerable<string> files)
    {
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                var sql = await File.ReadAllTextAsync(file);
                var checksum = ComputeSha256(sql);

                await using var cmd = new NpgsqlCommand($@"
                    INSERT INTO {MigrationTable} (filename, checksum, appliedat)
                    VALUES (@filename, @checksum, NOW())
                    ON CONFLICT (filename) DO NOTHING;", connection, tx);
                cmd.Parameters.AddWithValue("filename", fileName);
                cmd.Parameters.AddWithValue("checksum", checksum);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}