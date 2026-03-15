using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using App.Models;
using App.Services;
using System.Linq;

namespace App.Views;

public partial class HomePage : ContentPage
{
    ApiService api = new();
    AudioService audio = new();
    PoiAudioManager audioManager = new();

    List<Poi> poiList = new();

    Poi? selectedPoi;

    bool tracking = false;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Init();
    }

    async Task Init()
    {
        await GetLocation();
        await LoadPois();
        StartTracking();
    }

    async Task GetLocation()
    {
        try
        {
            var request = new GeolocationRequest(
                GeolocationAccuracy.Medium,
                TimeSpan.FromSeconds(10));

            var location = await Geolocation.GetLocationAsync(request);

            if (location == null)
                return;

            var pos = new Location(location.Latitude, location.Longitude);

            MixiMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    pos,
                    Distance.FromKilometers(1)));
        }
        catch
        {
        }
    }

    async Task LoadPois()
    {
        poiList = await api.GetPois();

        foreach (var poi in poiList)
        {
            AddPin(poi);
        }
    }

    void AddPin(Poi poi)
    {
        var pin = new Pin
        {
            Label = poi.Name,
            Location = new Location(poi.Latitude, poi.Longitude)
        };

        pin.MarkerClicked += (s, e) =>
        {
            selectedPoi = poi;

            PlaceName.Text = poi.Name;

            if (!string.IsNullOrEmpty(poi.ImageUrl))
                PlaceImage.Source = poi.ImageUrl;

            MoveToLocation(poi);
        };

        MixiMap.Pins.Add(pin);
    }

    void MoveToLocation(Poi poi)
    {
        var location = new Location(poi.Latitude, poi.Longitude);

        MixiMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromMeters(500)));
    }

    void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue?.ToLower() ?? "";

        var result = poiList
            .Where(p => p.Name.ToLower().Contains(keyword))
            .ToList();

        ReloadPins(result);
    }

    void ReloadPins(List<Poi> list)
    {
        MixiMap.Pins.Clear();

        foreach (var poi in list)
        {
            AddPin(poi);
        }
    }

    async void OnNavigateClicked(object sender, EventArgs e)
    {
        if (selectedPoi == null)
            return;

        string url =
            $"https://www.google.com/maps/dir/?api=1&destination={selectedPoi.Latitude},{selectedPoi.Longitude}";

        await Launcher.Default.OpenAsync(url);
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        if (selectedPoi == null)
            return;

        await audio.Play(selectedPoi.AudioUrl);
    }

    void OnPauseClicked(object sender, EventArgs e)
    {
        audio.Pause();
    }

    void StartTracking()
    {
        if (tracking)
            return;

        tracking = true;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(5), () =>
        {
            _ = TrackLocation();
            return true;
        });
    }

    async Task TrackLocation()
    {
        var location = await Geolocation.GetLocationAsync();

        if (location == null)
            return;

        var nearest = FindNearestPoi(location);

        if (nearest == null)
            return;

        if (!audioManager.CanPlay(nearest.Id))
            return;

        selectedPoi = nearest;

        PlaceName.Text = nearest.Name;

        if (!string.IsNullOrEmpty(nearest.ImageUrl))
            PlaceImage.Source = nearest.ImageUrl;

        await audio.Play(nearest.AudioUrl);
    }

    Poi? FindNearestPoi(Location user)
    {
        Poi? nearest = null;

        double minDistance = double.MaxValue;

        foreach (var poi in poiList)
        {
            var poiLocation = new Location(poi.Latitude, poi.Longitude);

            double distance =
                Location.CalculateDistance(
                    user,
                    poiLocation,
                    DistanceUnits.Kilometers) * 1000;

            if (distance < poi.Radius && distance < minDistance)
            {
                minDistance = distance;
                nearest = poi;
            }
        }

        return nearest;
    }
}