using Dapper;
using Npgsql;

namespace StreetFood.API.Services;

public static class VendorAuthHelper
{
    public static async Task<int?> GetVendorUserId(string connectionString, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QueryFirstOrDefaultAsync<int?>(@"
            SELECT id
            FROM users
            WHERE username = @U
              AND password = @P
              AND role = 'vendor'
              AND COALESCE(ishidden, FALSE) = FALSE",
            new { U = username, P = password });
    }
}
