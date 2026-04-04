using Microsoft.Maui.Networking;

namespace App.Services;

public static class NetworkReachability
{
    /// <summary>Cho phép gọi API qua Wi‑Fi LAN tới PC (Android thường báo Local, không phải Internet).</summary>
    public static bool HasUsableConnection =>
        Connectivity.Current.NetworkAccess is NetworkAccess.Internet
            or NetworkAccess.ConstrainedInternet
            or NetworkAccess.Local;
}
