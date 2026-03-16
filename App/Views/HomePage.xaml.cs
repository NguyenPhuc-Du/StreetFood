using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using App.Models;
using App.Services;

namespace App.Views;

public partial class HomePage : ContentPage
{
    ApiService api = new();
    List<Poi> poiList = new();
    Poi? selectedPoi;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPois();
    }

    // Hàm này can thiệp vào Android để ẩn nút Zoom và nút Định vị mặc định
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if ANDROID
        var mapControl = MixiMap.Handler?.PlatformView as Android.Gms.Maps.MapView;
        mapControl?.GetMapAsync(new MapCallback((googleMap) =>
        {
            googleMap.UiSettings.ZoomControlsEnabled = false; // Ẩn nút +/- 
            googleMap.UiSettings.MyLocationButtonEnabled = false; // Ẩn nút tròn định vị mặc định
        }));
#endif
    }

    async Task LoadPois()
    {
        try
        {
            poiList = await api.GetPois();
            MixiMap.Pins.Clear();
            foreach (var poi in poiList)
            {
                var pin = new Pin
                {
                    Label = poi.Name,
                    Location = new Location(poi.Latitude, poi.Longitude),
                    Type = PinType.Place
                };
                pin.MarkerClicked += (s, e) => {
                    selectedPoi = poi;
                    PlaceName.Text = poi.Name;
                    PlaceImage.Source = string.IsNullOrEmpty(poi.ImageUrl) ? "logo.png" : poi.ImageUrl;
                    InfoCard.IsVisible = true;
                };
                MixiMap.Pins.Add(pin);
            }
        }
        catch { }
    }

    async void OnGetLocationClicked(object sender, EventArgs e)
    {
        try
        {
            var location = await Geolocation.GetLocationAsync();
            if (location != null)
                MixiMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.5)));
        }
        catch { }
    }

    void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue?.ToLower() ?? "";
        // Thêm logic lọc Pin nếu cần
    }

    async void OnNavigateClicked(object sender, EventArgs e)
    {
        if (selectedPoi != null)
            await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(
                new Location(selectedPoi.Latitude, selectedPoi.Longitude),
                new MapLaunchOptions { Name = selectedPoi.Name });
    }

    void OnPlayClicked(object sender, EventArgs e) { /* Play Audio */ }
    void OnPauseClicked(object sender, EventArgs e) { /* Pause Audio */ }
}

// Lớp hỗ trợ xử lý Google Map trên Android (Dán vào cuối file HomePage.xaml.cs)
#if ANDROID
public class MapCallback : Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback
{
    private readonly Action<Android.Gms.Maps.GoogleMap> _onMapReady;
    public MapCallback(Action<Android.Gms.Maps.GoogleMap> onMapReady) => _onMapReady = onMapReady;
    public void OnMapReady(Android.Gms.Maps.GoogleMap googleMap) => _onMapReady(googleMap);
}
#endif