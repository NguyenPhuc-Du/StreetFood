using Dapper;
using Microsoft.CognitiveServices.Speech;
using Npgsql;

namespace StreetFood.API.Services;

/// <summary>
/// Chỉ dùng script tiếng Việt trong poi_translations → dịch 4 ngôn ngữ còn lại (Azure Translator) → ghi DB → TTS 5 file MP3 (Azure Speech).
/// </summary>
public class AzureSpeechTtsService
{
    private static readonly string[] OtherLangs = ["en", "cn", "ja", "ko"];
    private static readonly string[] AllAudioLangs = ["vi", "en", "cn", "ja", "ko"];

    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly AzureTranslatorClient _translator;
    private readonly R2StorageService _r2;
    private readonly ILogger<AzureSpeechTtsService> _logger;
    private readonly string _connStr;

    public AzureSpeechTtsService(
        IConfiguration config,
        IWebHostEnvironment env,
        AzureTranslatorClient translator,
        R2StorageService r2,
        ILogger<AzureSpeechTtsService> logger)
    {
        _config = config;
        _env = env;
        _translator = translator;
        _r2 = r2;
        _logger = logger;
        _connStr = config.GetConnectionString("DefaultConnection") ?? "";
    }

    private static (string Locale, string Voice) VoiceForDbLang(string dbLang)
    {
        return dbLang.ToLowerInvariant() switch
        {
            "vi" => ("vi-VN", "vi-VN-HoaiMyNeural"),
            "en" => ("en-US", "en-US-JennyNeural"),
            "cn" => ("zh-CN", "zh-CN-XiaoxiaoNeural"),
            "ja" => ("ja-JP", "ja-JP-NanamiNeural"),
            "ko" => ("ko-KR", "ko-KR-SunHiNeural"),
            _ => ("en-US", "en-US-JennyNeural")
        };
    }

    /// <summary>
    /// Lấy bản script tiếng Việt → dịch en/cn/ja/ko → cập nhật poi_translations → tạo MP3 cho 5 ngôn ngữ → restaurant_audio.
    /// </summary>
    public async Task<AudioGenerationResult> GenerateForPoiAsync(int poiId, CancellationToken cancellationToken = default)
    {
        var speechKey = _config["Azure:Speech:Key"];
        var region = _config["Azure:Speech:Region"] ?? "eastus";
        var publicBase = (_config["Api:PublicBaseUrl"] ?? "http://localhost:5288").TrimEnd('/');

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);

        var viText = await conn.QueryFirstOrDefaultAsync<string?>(@"
            SELECT description
            FROM poi_translations
            WHERE poiid = @Id AND languagecode = 'vi'",
            new { Id = poiId });

        if (string.IsNullOrWhiteSpace(viText))
        {
            return new AudioGenerationResult(false, 0, "Không có script tiếng Việt (poi_translations, languagecode = vi).");
        }

        viText = viText.Trim();
        if (viText.Length > 5000)
            viText = viText[..5000];

        Dictionary<string, string> translated;
        try
        {
            translated = await _translator.TranslateToLanguagesAsync(viText, "vi", OtherLangs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dịch từ tiếng Việt sang 4 ngôn ngữ thất bại (POI {PoiId})", poiId);
            return new AudioGenerationResult(false, 0, "Lỗi Azure Translator: " + ex.Message);
        }

        foreach (var lang in OtherLangs)
        {
            var txt = translated.TryGetValue(lang, out var t) && !string.IsNullOrWhiteSpace(t) ? t.Trim() : viText;
            if (txt.Length > 5000)
                txt = txt[..5000];

            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE poi_translations SET description = @Desc
                WHERE poiid = @PoiId AND languagecode = @Lang",
                new { Desc = txt, PoiId = poiId, Lang = lang },
                cancellationToken: cancellationToken));
        }

        var textsByLang = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vi"] = viText
        };
        foreach (var lang in OtherLangs)
        {
            var txt = translated.TryGetValue(lang, out var t) && !string.IsNullOrWhiteSpace(t) ? t.Trim() : viText;
            if (txt.Length > 5000)
                txt = txt[..5000];
            textsByLang[lang] = txt;
        }

        if (string.IsNullOrWhiteSpace(speechKey))
        {
            _logger.LogWarning("Azure:Speech:Key trống — đã cập nhật 4 bản dịch từ script VI, không tạo MP3 (POI {PoiId}).", poiId);
            return new AudioGenerationResult(true, 0, "Đã duyệt script và cập nhật mô tả en/cn/ja/ko. Chưa tạo MP3 vì chưa cấu hình Azure Speech Key.");
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return await SynthesizeToRestaurantAudioAsync(
            conn, poiId, textsByLang, speechKey, region, publicBase, webRoot, translateJustApplied: true, cancellationToken);
    }

    /// <summary>
    /// Chỉ đọc mô tả đã lưu trong <c>poi_translations</c> rồi tạo MP3 — dùng sau hàng đợi (đã dịch trong request phê duyệt tạo POI).
    /// </summary>
    public async Task<AudioGenerationResult> GenerateTtsFromDatabaseOnlyAsync(int poiId, CancellationToken cancellationToken = default)
    {
        var speechKey = _config["Azure:Speech:Key"];
        var region = _config["Azure:Speech:Region"] ?? "eastus";
        var publicBase = (_config["Api:PublicBaseUrl"] ?? "http://localhost:5288").TrimEnd('/');

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(cancellationToken);

        var rows = (await conn.QueryAsync<(string Lang, string? Desc)>(new CommandDefinition(@"
            SELECT languagecode, description
            FROM poi_translations
            WHERE poiid = @Id AND languagecode IN ('vi','en','cn','ja','ko')",
            new { Id = poiId },
            cancellationToken: cancellationToken))).ToList();

        var textsByLang = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (lang, desc) in rows)
        {
            if (string.IsNullOrWhiteSpace(desc)) continue;
            var t = desc.Trim();
            if (t.Length > 5000) t = t[..5000];
            textsByLang[lang] = t;
        }

        if (!textsByLang.ContainsKey("vi") || string.IsNullOrWhiteSpace(textsByLang["vi"]))
            return new AudioGenerationResult(false, 0, "Không có mô tả tiếng Việt (poi_translations).");

        if (string.IsNullOrWhiteSpace(speechKey))
        {
            _logger.LogWarning("Azure:Speech:Key trống — queue TTS không tạo MP3 (POI {PoiId}).", poiId);
            return new AudioGenerationResult(true, 0, "Chưa tạo MP3 vì chưa cấu hình Azure Speech Key.");
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return await SynthesizeToRestaurantAudioAsync(
            conn, poiId, textsByLang, speechKey, region, publicBase, webRoot, translateJustApplied: false, cancellationToken);
    }

    private async Task<AudioGenerationResult> SynthesizeToRestaurantAudioAsync(
        NpgsqlConnection conn,
        int poiId,
        Dictionary<string, string> textsByLang,
        string speechKey,
        string region,
        string publicBase,
        string webRoot,
        bool translateJustApplied,
        CancellationToken cancellationToken)
    {
        var audioDir = Path.Combine(webRoot, "audio");
        if (!_r2.IsEnabled)
            Directory.CreateDirectory(audioDir);

        var ok = 0;
        var errors = new List<string>();

        foreach (var lang in AllAudioLangs)
        {
            if (!textsByLang.TryGetValue(lang, out var text) || string.IsNullOrWhiteSpace(text))
                continue;

            text = text.Trim();
            if (text.Length > 5000)
                text = text[..5000];

            try
            {
                var (locale, voiceName) = VoiceForDbLang(lang);
                var speechConfig = SpeechConfig.FromSubscription(speechKey, region);
                speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz128KBitRateMonoMp3);
                speechConfig.SpeechSynthesisLanguage = locale;
                speechConfig.SpeechSynthesisVoiceName = voiceName;

                using var synthesizer = new SpeechSynthesizer(speechConfig, null);
                var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false);

                if (result.Reason != ResultReason.SynthesizingAudioCompleted)
                {
                    var err = result.Reason + ": " + result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult, "");
                    errors.Add($"{lang}: {err}");
                    _logger.LogWarning("TTS POI {PoiId} lang {Lang}: {Reason}", poiId, lang, result.Reason);
                    continue;
                }

                byte[] audioData = result.AudioData ?? Array.Empty<byte>();
                if (audioData.Length == 0)
                {
                    errors.Add($"{lang}: không có dữ liệu âm thanh.");
                    continue;
                }

                var fileName = $"poi_{poiId}_{lang}.mp3";
                string url;
                if (_r2.IsEnabled)
                {
                    await using var ms = new MemoryStream(audioData, writable: false);
                    var objectKey = $"{poiId}/audio/{lang}.mp3";
                    var r2Url = await _r2.UploadAsync(objectKey, ms, "audio/mpeg", cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(r2Url))
                    {
                        errors.Add($"{lang}: upload R2 thất bại.");
                        continue;
                    }
                    url = r2Url;
                }
                else
                {
                    var path = Path.Combine(audioDir, fileName);
                    await File.WriteAllBytesAsync(path, audioData, cancellationToken).ConfigureAwait(false);
                    url = $"{publicBase}/audio/{fileName}";
                }

                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO restaurant_audio (poiid, languagecode, audiourl)
                    VALUES (@PoiId, @Lang, @Url)
                    ON CONFLICT (poiid, languagecode) DO UPDATE SET audiourl = EXCLUDED.audiourl",
                    new { PoiId = poiId, Lang = lang, Url = url },
                    cancellationToken: cancellationToken));

                ok++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TTS lỗi POI {PoiId} ngôn ngữ {Lang}", poiId, lang);
                errors.Add($"{lang}: {ex.Message}");
            }
        }

        var msg = ok > 0
            ? (translateJustApplied
                ? $"Đã cập nhật 4 bản dịch từ script VI và tạo {ok} file MP3 (5 ngôn ngữ)."
                : $"Đã tạo {ok} file MP3 từ mô tả hiện có (hàng đợi TTS).")
            : (translateJustApplied
                ? "Đã cập nhật bản dịch; không tạo được file MP3."
                : "Không tạo được file MP3 từ hàng đợi.");
        if (errors.Count > 0)
            msg += " Chi tiết: " + string.Join("; ", errors.Take(5));

        return new AudioGenerationResult(ok > 0, ok, msg);
    }
}

public record AudioGenerationResult(bool Success, int FilesWritten, string Message);
