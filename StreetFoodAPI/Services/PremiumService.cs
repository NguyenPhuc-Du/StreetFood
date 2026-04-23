using System.Threading;
using Dapper;
using Npgsql;

namespace StreetFood.API.Services;

public sealed class PremiumStatusRow
{
    public bool IsPremium { get; set; }
    public string PlanName { get; set; } = "thuong";
    public DateTime? EndsAtUtc { get; set; }
}

public sealed class PremiumService
{
    private readonly string _connStr;
    private readonly ILogger<PremiumService> _logger;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static int _schemaReady;

    public PremiumService(IConfiguration config, ILogger<PremiumService> logger)
    {
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1)
            return;

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _schemaReady) == 1)
                return;

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(cancellationToken);
            var ready = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(@"
                SELECT
                    to_regclass('public.pois') IS NOT NULL
                    AND to_regclass('public.poi_premium_subscriptions') IS NOT NULL
                    AND to_regclass('public.vendor_payment_orders') IS NOT NULL;", cancellationToken: cancellationToken));
            if (!ready)
                throw new InvalidOperationException(
                    "Thiếu bảng premium/payment. Hãy bật Database:ApplySqlScriptsOnStartup=true và chạy lại bộ migration từ đầu.");
            Interlocked.Exchange(ref _schemaReady, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PremiumService.EnsureSchemaAsync failed");
            throw;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public async Task<PremiumStatusRow> GetPoiPremiumStatusAsync(int poiId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);

        var row = await conn.QueryFirstOrDefaultAsync<PremiumStatusRow>(new CommandDefinition(@"
            SELECT
                COALESCE((ps.status = 'active' AND ps.end_at > NOW()), FALSE) AS IsPremium,
                CASE
                    WHEN COALESCE((ps.status = 'active' AND ps.end_at > NOW()), FALSE) THEN COALESCE(ps.plan_name, 'premium')
                    ELSE 'thuong'
                END AS PlanName,
                ps.end_at AS EndsAtUtc
            FROM pois p
            LEFT JOIN LATERAL (
                SELECT plan_name, status, end_at
                FROM poi_premium_subscriptions
                WHERE poi_id = p.id
                ORDER BY end_at DESC NULLS LAST
                LIMIT 1
            ) ps ON TRUE
            WHERE p.id = @PoiId
            LIMIT 1",
            new { PoiId = poiId },
            cancellationToken: cancellationToken));

        if (row != null) return row;
        return new PremiumStatusRow { IsPremium = false, PlanName = "thuong", EndsAtUtc = null };
    }

    public async Task UpsertPaymentOrderAsync(
        string orderId,
        string requestId,
        int vendorUserId,
        int poiId,
        int amountVnd,
        string status,
        string rawResponseJson,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO vendor_payment_orders
                (order_id, request_id, vendor_user_id, poi_id, provider, amount_vnd, status, raw_response, expires_at)
            VALUES
                (@OrderId, @RequestId, @VendorUserId, @PoiId, 'momo', @AmountVnd, @Status, CAST(@RawResponseJson AS jsonb), NOW() + INTERVAL '2 days')
            ON CONFLICT (order_id) DO UPDATE SET
                request_id = EXCLUDED.request_id,
                status = EXCLUDED.status,
                raw_response = EXCLUDED.raw_response",
            new
            {
                OrderId = orderId,
                RequestId = requestId,
                VendorUserId = vendorUserId,
                PoiId = poiId,
                AmountVnd = amountVnd,
                Status = status,
                RawResponseJson = string.IsNullOrWhiteSpace(rawResponseJson) ? "{}" : rawResponseJson
            },
            cancellationToken: cancellationToken));
    }

    public async Task MarkOrderPaidAndActivateAsync(
        string orderId,
        string transId,
        string rawResponseJson,
        int premiumDays,
        int amountVnd,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var order = await conn.QueryFirstOrDefaultAsync<(int PoiId, string Status)>(new CommandDefinition(@"
            SELECT poi_id AS PoiId, status AS Status
            FROM vendor_payment_orders
            WHERE order_id = @OrderId
            LIMIT 1",
            new { OrderId = orderId },
            tx,
            cancellationToken: cancellationToken));

        if (order.PoiId <= 0)
            throw new InvalidOperationException("Không tìm thấy order cần kích hoạt premium.");

        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE vendor_payment_orders
            SET status = 'paid',
                trans_id = @TransId,
                paid_at = NOW(),
                raw_response = CAST(@RawResponseJson AS jsonb)
            WHERE order_id = @OrderId",
            new
            {
                OrderId = orderId,
                TransId = transId,
                RawResponseJson = string.IsNullOrWhiteSpace(rawResponseJson) ? "{}" : rawResponseJson
            },
            tx,
            cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO poi_premium_subscriptions
                (poi_id, plan_name, status, price_vnd, started_at, end_at, last_order_id, updated_at)
            VALUES
                (@PoiId, 'premium', 'active', @AmountVnd, NOW(), NOW() + (@Days * INTERVAL '1 day'), @OrderId, NOW())
            ON CONFLICT (poi_id) DO UPDATE SET
                plan_name = 'premium',
                status = 'active',
                price_vnd = @AmountVnd,
                started_at = NOW(),
                end_at = CASE
                    WHEN poi_premium_subscriptions.status = 'active' AND poi_premium_subscriptions.end_at > NOW()
                        THEN poi_premium_subscriptions.end_at + (@Days * INTERVAL '1 day')
                    ELSE NOW() + (@Days * INTERVAL '1 day')
                END,
                last_order_id = @OrderId,
                updated_at = NOW()",
            new
            {
                PoiId = order.PoiId,
                AmountVnd = amountVnd,
                Days = Math.Max(1, premiumDays),
                OrderId = orderId
            },
            tx,
            cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE pois p
            SET is_premium = CASE WHEN ps.status = 'active' AND ps.end_at > NOW() THEN TRUE ELSE FALSE END,
                premium_end_at = ps.end_at,
                last_premium_order_id = ps.last_order_id
            FROM poi_premium_subscriptions ps
            WHERE p.id = ps.poi_id
              AND p.id = @PoiId",
            new { PoiId = order.PoiId },
            tx,
            cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
    }

    public async Task MarkOrderStatusAsync(
        string orderId,
        string status,
        string rawResponseJson,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE vendor_payment_orders
            SET status = @Status,
                raw_response = CAST(@RawResponseJson AS jsonb)
            WHERE order_id = @OrderId",
            new
            {
                OrderId = orderId,
                Status = status,
                RawResponseJson = string.IsNullOrWhiteSpace(rawResponseJson) ? "{}" : rawResponseJson
            },
            cancellationToken: cancellationToken));
    }
}
