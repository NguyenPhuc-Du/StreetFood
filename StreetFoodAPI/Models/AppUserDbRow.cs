namespace StreetFood.API.Models;

public class AppUserDbRow
{
    public int id { get; set; }
    public string username { get; set; } = "";
    public DateTime? app_activation_expires_at { get; set; }
}
