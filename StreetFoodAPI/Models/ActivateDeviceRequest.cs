namespace StreetFood.API.Models;

public class ActivateDeviceRequest
{
    public string InstallId { get; set; } = "";
    public string ActivationCode { get; set; } = "";
}
