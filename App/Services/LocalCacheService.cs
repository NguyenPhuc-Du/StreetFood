using App.Models;
using Microsoft.Maui.Storage;
using SQLite;
using System.Text.Json;

namespace App.Services;

public sealed class LocalCacheService
{
    private readonly SQLiteAsyncConnection _db;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public LocalCacheService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "streetfood-cache.db3");
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        await _db.CreateTableAsync<PoiListCacheRecord>();
        await _db.CreateTableAsync<PoiDetailCacheRecord>();
    }

    public async Task SavePoisAsync(string languageCode, List<Poi> pois)
    {
        var payload = JsonSerializer.Serialize(pois, _jsonOptions);
        var record = new PoiListCacheRecord
        {
            LanguageCode = languageCode,
            PayloadJson = payload,
            UpdatedAtUtcTicks = DateTime.UtcNow.Ticks
        };
        await _db.InsertOrReplaceAsync(record);
    }

    public async Task<List<Poi>> GetPoisAsync(string languageCode)
    {
        var record = await _db.Table<PoiListCacheRecord>()
            .Where(x => x.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        if (record == null || string.IsNullOrWhiteSpace(record.PayloadJson))
            return new List<Poi>();

        return JsonSerializer.Deserialize<List<Poi>>(record.PayloadJson, _jsonOptions) ?? new List<Poi>();
    }

    public async Task SavePoiDetailAsync(string languageCode, PoiDetail detail)
    {
        var payload = JsonSerializer.Serialize(detail, _jsonOptions);
        var record = new PoiDetailCacheRecord
        {
            CacheKey = BuildDetailKey(languageCode, detail.Id),
            LanguageCode = languageCode,
            PoiId = detail.Id,
            PayloadJson = payload,
            UpdatedAtUtcTicks = DateTime.UtcNow.Ticks
        };
        await _db.InsertOrReplaceAsync(record);
    }

    public async Task<PoiDetail?> GetPoiDetailAsync(string languageCode, int poiId)
    {
        var key = BuildDetailKey(languageCode, poiId);
        var record = await _db.FindAsync<PoiDetailCacheRecord>(key);
        if (record == null || string.IsNullOrWhiteSpace(record.PayloadJson))
            return null;

        return JsonSerializer.Deserialize<PoiDetail>(record.PayloadJson, _jsonOptions);
    }

    private static string BuildDetailKey(string languageCode, int poiId) => $"{languageCode}:{poiId}";

    [Table("PoiListCache")]
    private sealed class PoiListCacheRecord
    {
        [PrimaryKey]
        public string LanguageCode { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public long UpdatedAtUtcTicks { get; set; }
    }

    [Table("PoiDetailCache")]
    private sealed class PoiDetailCacheRecord
    {
        [PrimaryKey]
        public string CacheKey { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public int PoiId { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public long UpdatedAtUtcTicks { get; set; }
    }
}
