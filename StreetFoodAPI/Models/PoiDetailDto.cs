namespace StreetFood.API.Models;

public class PoiDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? ImageUrl { get; set; }
    /// <summary>Địa chỉ đường (POIs.address).</summary>
    public string? Address { get; set; }
    /// <summary>Mô tả giới thiệu đa ngôn ngữ (POI_Translations.description).</summary>
    public string? Description { get; set; }
    public string? OpeningHours { get; set; }
    public string? Phone { get; set; }
    public string? AudioUrl { get; set; }

    public List<FoodDto> Foods { get; set; } = new();
}

