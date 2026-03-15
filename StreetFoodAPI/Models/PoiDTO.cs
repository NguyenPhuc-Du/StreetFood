namespace StreetFood.API.Models;

public class PoiDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string ImageUrl { get; set; }

    public string AudioUrl { get; set; }
}