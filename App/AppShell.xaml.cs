using App.Views;

namespace App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Điều hướng trang chi tiết POI dùng query string: poidetail?poiId=1
        Routing.RegisterRoute("poidetail", typeof(PoiDetailPage));
    }
}
