using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StreetFood.API.Services;

public sealed class AudioPipelineOptions
{
    public const string SectionName = "AudioPipeline";
    public bool Enabled { get; set; } = true;
    public bool UseQueue { get; set; } = true;
    public bool WorkerEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 3;
    public int StaleProcessingMinutes { get; set; } = 20;
}

public sealed class AudioPipelineJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioPipelineJobStore _store;
    private readonly IOptions<AudioPipelineOptions> _options;
    private readonly ILogger<AudioPipelineJobWorker> _logger;
    private int _tick;

    public AudioPipelineJobWorker(
        IServiceScopeFactory scopeFactory,
        AudioPipelineJobStore store,
        IOptions<AudioPipelineOptions> options,
        ILogger<AudioPipelineJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("AudioPipeline:Enabled=false — worker không chạy.");
            return;
        }

        try
        {
            await _store.EnsureSchemaAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không khởi tạo bảng audio_pipeline_jobs. Worker tạm dừng.");
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Clamp(_options.Value.PollSeconds, 1, 60));
        var stale = TimeSpan.FromMinutes(Math.Clamp(_options.Value.StaleProcessingMinutes, 1, 240));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_options.Value.WorkerEnabled)
            {
                await Task.Delay(poll, stoppingToken);
                continue;
            }

            try
            {
                if (++_tick % 10 == 0)
                    await _store.ResetStaleProcessingAsync(stale, stoppingToken);

                var job = await _store.TryClaimNextAsync(stoppingToken);
                if (job == null)
                {
                    await Task.Delay(poll, stoppingToken);
                    continue;
                }

                await ProcessOneAsync(job, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi vòng lặp AudioPipeline job worker");
                await Task.Delay(poll, stoppingToken);
            }
        }
    }

    private async Task ProcessOneAsync(AudioPipelineJobRow job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(job.PayloadJson))
        {
            await _store.MarkFailureAsync(job.Id, "payload trống", job.AttemptCount + 1, cancellationToken);
            return;
        }

        using var doc = JsonDocument.Parse(job.PayloadJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("poiId", out var pEl) || pEl.GetInt32() is <= 0)
        {
            await _store.MarkFailureAsync(job.Id, "payload.poiId không hợp lệ", job.AttemptCount + 1, cancellationToken);
            return;
        }

        var poiId = pEl.GetInt32();
        var mode = root.TryGetProperty("mode", out var m) ? m.GetString() : AudioPipelineJobStore.ModeTtsFromDb;
        if (string.IsNullOrEmpty(mode)) mode = AudioPipelineJobStore.ModeTtsFromDb;

        using var scope = _scopeFactory.CreateScope();
        var tts = scope.ServiceProvider.GetRequiredService<AzureSpeechTtsService>();

        try
        {
            if (string.Equals(mode, AudioPipelineJobStore.ModeFullRegenerate, StringComparison.OrdinalIgnoreCase))
            {
                _ = await tts.GenerateForPoiAsync(poiId, cancellationToken);
            }
            else
            {
                _ = await tts.GenerateTtsFromDatabaseOnlyAsync(poiId, cancellationToken);
            }

            await _store.MarkSuccessAsync(job.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS job {JobId} POI {PoiId} thất bại", job.Id, poiId);
            await _store.MarkFailureAsync(job.Id, ex.Message, job.AttemptCount + 1, cancellationToken);
        }
    }
}
