namespace App.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Radius { get; set; } = 50;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string? OpeningHours { get; set; } //
    public string? AudioUrl { get; set; }     //
    public int VisitCount { get; set; }
}