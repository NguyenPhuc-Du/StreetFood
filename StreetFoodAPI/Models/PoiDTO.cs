namespace StreetFood.API.Models;

public class PoiDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; } = 50;
    public string? ImageUrl { get; set; }
    /// <summary>Địa chỉ đường (cột POIs.address).</summary>
    public string? Address { get; set; }
    /// <summary>Nội dung giới thiệu / mô tả (POI_Translations.description).</summary>
    public string? Description { get; set; }
    public string? OpeningHours { get; set; } 
    public string? AudioUrl { get; set; }
    public bool IsPremium { get; set; }
}