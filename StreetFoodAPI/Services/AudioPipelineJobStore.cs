using System.Text.Json;
using System.Threading;
using Dapper;
using Npgsql;
using StreetFood.API.Models.Admin;

namespace StreetFood.API.Services;

public sealed class AudioPipelineJobStore
{
    public const string JobTypeTtsPoi = "tts_poi";
    public const string ModeTtsFromDb = "tts_only";
    public const string ModeFullRegenerate = "full_regenerate";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _connStr;
    private readonly ILogger<AudioPipelineJobStore> _logger;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static int _schemaReady;

    public AudioPipelineJobStore(IConfiguration config, ILogger<AudioPipelineJobStore> logger)
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

            const string sql = @"
CREATE TABLE IF NOT EXISTS audio_pipeline_jobs (
    id              BIGSERIAL PRIMARY KEY,
    idempotency_key TEXT,
    job_type        TEXT        NOT NULL DEFAULT 'tts_poi',
    payload         JSONB       NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'pending',
    attempt_count   INT         NOT NULL DEFAULT 0,
    last_error      TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    next_run_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS audio_pipeline_jobs_idem_uq
    ON audio_pipeline_jobs (idempotency_key)
    WHERE idempotency_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS audio_pipeline_jobs_status_next_idx
    ON audio_pipeline_jobs (status, next_run_at)
    WHERE status IN ('pending', 'processing', 'retrying');";

            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(cancellationToken);
            await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            Interlocked.Exchange(ref _schemaReady, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnsureSchema audio_pipeline_jobs");
            throw;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    /// <returns>Id job; nếu trùng idempotency và job chưa xong thì trả id cũ.</returns>
    public async Task<long> EnqueueTtsPoiAsync(int poiId, string mode, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["poiId"] = poiId,
            ["mode"] = mode
        }, JsonOpts);

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await conn.QueryFirstOrDefaultAsync<long?>(@"
                SELECT id FROM audio_pipeline_jobs
                WHERE idempotency_key = @K
                  AND status IN ('pending', 'processing', 'retrying')
                LIMIT 1",
                new { K = idempotencyKey });
            if (existing.HasValue)
                return existing.Value;
        }

        try
        {
            return await conn.QuerySingleAsync<long>(@"
            INSERT INTO audio_pipeline_jobs (idempotency_key, job_type, payload, status, next_run_at)
            VALUES (@K, @Type, @Payload::jsonb, 'pending', NOW())
            RETURNING id",
                new
                {
                    K = idempotencyKey,
                    Type = JobTypeTtsPoi,
                    Payload = payload
                });
        }
        catch (PostgresException ex) when (ex.SqlState == "23505" && !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var eid = await conn.QueryFirstOrDefaultAsync<long?>(@"
                SELECT id FROM audio_pipeline_jobs WHERE idempotency_key = @K",
                new { K = idempotencyKey });
            if (eid.HasValue) return eid.Value;
            throw;
        }
    }

    public async Task<AudioPipelineJobRow?> TryClaimNextAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        return await conn.QueryFirstOrDefaultAsync<AudioPipelineJobRow>(new CommandDefinition(@"
WITH cte AS (
  SELECT id FROM audio_pipeline_jobs
  WHERE status IN ('pending', 'retrying') AND next_run_at <= NOW()
  ORDER BY next_run_at, id
  FOR UPDATE SKIP LOCKED
  LIMIT 1
)
UPDATE audio_pipeline_jobs j
SET status = 'processing',
    started_at = COALESCE(j.started_at, NOW())
FROM cte
WHERE j.id = cte.id
RETURNING j.id, j.idempotency_key AS IdempotencyKey, j.job_type AS JobType, j.payload::text AS PayloadJson, j.status, j.attempt_count AS AttemptCount, j.last_error AS LastError, j.created_at AS CreatedAt, j.next_run_at AS NextRunAt;",
            cancellationToken: cancellationToken));
    }

    public async Task MarkSuccessAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE audio_pipeline_jobs
            SET status = 'success', completed_at = NOW(), last_error = NULL
            WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public static TimeSpan BackoffForAttempt(int attemptCountAfterFailure)
    {
        return attemptCountAfterFailure switch
        {
            <= 1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
    }

    public const int MaxAttempts = 5;

    public async Task ResetStaleProcessingAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        var sec = (int)Math.Min(olderThan.TotalSeconds, int.MaxValue);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE audio_pipeline_jobs
            SET status = 'retrying', next_run_at = NOW(), started_at = NULL, last_error = 'stale_processing_reset'
            WHERE status = 'processing' AND started_at < (NOW() - @Sec * INTERVAL '1 second')",
            new { Sec = sec },
            cancellationToken: cancellationToken));
    }

    public async Task MarkFailureAsync(long id, string error, int newAttemptCount, CancellationToken cancellationToken = default)
    {
        var dead = newAttemptCount >= MaxAttempts;
        var next = dead ? (DateTime?)null : DateTime.UtcNow.Add(BackoffForAttempt(newAttemptCount));

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        if (dead)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE audio_pipeline_jobs
                SET status = 'dead_letter', attempt_count = @N, last_error = @Err, completed_at = NOW()
                WHERE id = @Id",
                new { Id = id, N = newAttemptCount, Err = Trunc(error) },
                cancellationToken: cancellationToken));
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE audio_pipeline_jobs
                SET status = 'retrying', attempt_count = @N, last_error = @Err, next_run_at = @Next, started_at = NULL
                WHERE id = @Id",
                new { Id = id, N = newAttemptCount, Err = Trunc(error), Next = next },
                cancellationToken: cancellationToken));
        }
    }

    private static string? Trunc(string? s) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= 2000 ? s : s[..2000]);

    public async Task<AudioJobQueueStatsDto> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        return await conn.QuerySingleAsync<AudioJobQueueStatsDto>(new CommandDefinition(@"
            SELECT
                (SELECT COUNT(*)::bigint FROM audio_pipeline_jobs WHERE status = 'pending')      AS PendingCount,
                (SELECT COUNT(*)::bigint FROM audio_pipeline_jobs WHERE status = 'processing') AS ProcessingCount,
                (SELECT COUNT(*)::bigint FROM audio_pipeline_jobs WHERE status = 'retrying')  AS RetryingCount,
                (SELECT COUNT(*)::bigint FROM audio_pipeline_jobs WHERE status = 'success' AND completed_at > NOW() - INTERVAL '24 hours') AS Success24h,
                (SELECT COUNT(*)::bigint FROM audio_pipeline_jobs WHERE status = 'dead_letter' AND completed_at > NOW() - INTERVAL '24 hours') AS Dead24h,
                (SELECT EXTRACT(EPOCH FROM (NOW() - MIN(created_at)))::double precision
                 FROM audio_pipeline_jobs WHERE status IN ('pending', 'retrying') AND next_run_at <= NOW()) AS OldestReadyWaitSeconds;
            ",
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AudioJobListItemDto>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        await EnsureSchemaAsync(cancellationToken);
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);
        var rows = await conn.QueryAsync<AudioJobListItemDto>(new CommandDefinition(@"
            SELECT
                id,
                idempotency_key,
                job_type,
                status,
                attempt_count,
                LEFT(COALESCE(last_error, ''), 200) AS ""LastErrorPreview"",
                created_at,
                completed_at
            FROM audio_pipeline_jobs
            ORDER BY id DESC
            LIMIT @Limit",
            new { Limit = limit },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}

public sealed class AudioPipelineJobRow
{
    public long Id { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? JobType { get; set; }
    public string? PayloadJson { get; set; }
    public string? Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextRunAt { get; set; }
}
