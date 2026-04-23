namespace StreetFood.API.Models.Admin;

public class CreatePoiOwnerRequest
{
    public string OwnerUsername { get; set; } = "";
    public string OwnerPassword { get; set; } = "";
    public string? OwnerEmail { get; set; }
    public string PoiName { get; set; } = "";
    public string? PoiDescription { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; } = 50;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public string? OpeningHours { get; set; }
    public string? Phone { get; set; }
    public string? InitialScript { get; set; }
}

public record HeatPointDto(double Lat, double Lng, int Weight);

public record PathSegmentDto(
    int Id,
    string DeviceId,
    int FromPoiId,
    int ToPoiId,
    DateTime CreatedAt,
    double FromLat,
    double FromLng,
    double ToLat,
    double ToLng,
    string? FromName,
    string? ToName);

/// <summary>Cáº·p Fromâ†’To Ä‘Æ°á»£c ghi nháº­n nhiá»u nháº¥t (dÃ¹ng váº½ tuyáº¿n Ä‘Æ°á»ng).</summary>
public record PopularPathDto(
    int FromPoiId,
    int ToPoiId,
    int TripCount,
    double FromLat,
    double FromLng,
    double ToLat,
    double ToLng,
    string? FromName,
    string? ToName);

public record RouteChainPointDto(int PoiId, double Lat, double Lng, string? Name);

/// <summary>Chuá»—i POI (tá»‘i Ä‘a 5 Ä‘iá»ƒm) ghÃ©p tá»« Movement_Paths.</summary>
public record PopularRouteChainDto(int TripCount, string Summary, IReadOnlyList<RouteChainPointDto> Points);

public record AwaitingPoiDto(int PoiId, string PoiName, string OwnerUsername, string State, int AudioCount, bool IsPremium);

public record PendingScriptDto(
    int Id,
    int PoiId,
    string LanguageCode,
    string? NewScript,
    string Status,
    DateTime CreatedAt,
    string PoiName,
    string? SubmittedByUsername);

public record OwnerRowDto(
    int Id,
    string Username,
    string? Email,
    string Role,
    bool IsHidden,
    int? PoiId,
    string? PoiName);

public record DashboardSummaryDto(
    int PoiCount,
    int PremiumPoiCount,
    int VendorCount,
    int PendingScripts,
    int AudioTracks,
    int LocationSamples30d);

/// <summary>Thá»‘ng kÃª thá»i lÆ°á»£ng nghe audio trung bÃ¬nh theo POI (tá»« poi_audio_listen_events).</summary>
public record PoiAudioListenStatsDto(
    int PoiId,
    string? PoiName,
    long ListenSamples,
    double? AvgDurationSeconds);

public record HourlyActiveUsersDto(
    int HourOfDay,
    long UserCount);

/// <summary>Æ¯á»›c lÆ°á»£ng thiáº¿t bá»‹ Ä‘ang má»Ÿ app: distinct deviceId cÃ³ location_log trong cá»­a sá»• phÃºt gáº§n Ä‘Ã¢y.</summary>
public record OnlineNowDto(long UniqueDeviceCount, int WindowMinutes);

/// <summary>Snapshot váº­n hÃ nh nhanh cho API/ingest/DB.</summary>
public record OpsMetricsDto(
    DateTime GeneratedAtUtc,
    int PoiCount,
    long LocationLogs24h,
    long MovementPaths24h,
    long ListenEvents24h,
    DateTime? LastLocationAtUtc,
    DateTime? LastMovementAtUtc,
    DateTime? LastListenAtUtc);

public record AudioJobQueueStatsDto(
    long PendingCount,
    long ProcessingCount,
    long RetryingCount,
    long Success24h,
    long Dead24h,
    double? OldestReadyWaitSeconds);

public class AudioJobListItemDto
{
    public long Id { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? JobType { get; set; }
    public string? Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastErrorPreview { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public record PoiIngressQueueSettingsDto(
    bool Enabled,
    int MinDelayMs,
    int MaxDelayMs,
    long ContentionCount);

public class UpdatePoiIngressQueueRequest
{
    public bool? Enabled { get; set; }
    public int? MinDelayMs { get; set; }
    public int? MaxDelayMs { get; set; }
}