namespace StreetFood.API.Models;

public sealed class PoiListenEventRequest
{
    public int PoiId { get; set; }
    public int DurationSeconds { get; set; }
    public string? DeviceId { get; set; }
}
