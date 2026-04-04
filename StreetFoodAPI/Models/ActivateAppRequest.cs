namespace StreetFood.API.Models;

public class ActivateAppRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ActivationCode { get; set; } = "";
}
