using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace App.Views;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();

        GetLocation();
    }

    async void GetLocation()
    {
        var location = await Geolocation.GetLocationAsync();

        if (location != null)
        {
            var position = new Location(location.Latitude, location.Longitude);

            MixiMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    position,
                    Distance.FromKilometers(1)));
        }
    }

    void OnAllFilter(object sender, EventArgs e)
    {
        Console.WriteLine("All filter");
    }

    void OnShopFilter(object sender, EventArgs e)
    {
        Console.WriteLine("Shop filter");
    }

    void OnPoiFilter(object sender, EventArgs e)
    {
        Console.WriteLine("POI filter");
    }

    void OnFoodFilter(object sender, EventArgs e)
    {
        Console.WriteLine("Food filter");
    }

    void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Console.WriteLine("Searching: " + e.NewTextValue);
    }

    void OnLocationClicked(object sender, EventArgs e)
    {
        GetLocation();
    }

    async void OnNavigateClicked(object sender, EventArgs e)
    {
        await Launcher.Default.OpenAsync("https://maps.google.com");
    }
}