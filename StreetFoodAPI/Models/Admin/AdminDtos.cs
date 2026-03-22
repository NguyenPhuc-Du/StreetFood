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

public record AwaitingPoiDto(int PoiId, string PoiName, string OwnerUsername, string State, int AudioCount);

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
    int VendorCount,
    int PendingScripts,
    int AudioTracks,
    int LocationSamples30d);
