using App.Models;
using App.Services;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Threading;

namespace App.Views;

public partial class HomePage : ContentPage
{
    const string PinnedPoiKey = "pinnedPoiId";
    ApiService api = new();
    List<Poi> poiList = new();
    List<Poi> filteredPoiList = new();
    CancellationTokenSource? _cts;
    int currentAudioPoiId = -1;
    bool isFirstLocation = true;
    bool _isAutoPlaying = false;
    bool _manualSelectionBlocksAuto = false;
    Location? _currentUserLocation;
    Dictionary<int, DateTime> lastPlayed = new();
    const int COOLDOWN_SECONDS = 300;

    const string AutoAudioKey = "autoAudioEnabled";

    Poi? _currentPoi;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) return;

        try
        {
            var data = await api.GetPois();
            poiList = data ?? new();
            ApplyFiltersAndRefreshMap();
            TryShowPinnedPoi();
        }
        catch { }
        StartTracking();
    }

    private async void StartTracking()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best), _cts.Token);
                if (loc != null)
                {
                    _currentUserLocation = loc;
                    if (isFirstLocation)
                    {
                        MixiMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(500)));
                        isFirstLocation = false;
                    }
                    ApplyFiltersAndRefreshMap();
                    CheckNearby(loc);
                }
                await Task.Delay(4000, _cts.Token);
            }
        }
        catch { }
    }

    bool CanPlay(int poiId)
    {
        if (lastPlayed.ContainsKey(poiId))
        {
            var diff = (DateTime.Now - lastPlayed[poiId]).TotalSeconds;
            if (diff < COOLDOWN_SECONDS) return false;
        }
        lastPlayed[poiId] = DateTime.Now;
        return true;
    }

    private void CheckNearby(Location userLoc)
    {
        if (filteredPoiList == null || !filteredPoiList.Any()) return;

        var nearest = filteredPoiList
            .OrderBy(p => userLoc.CalculateDistance(new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers))
            .FirstOrDefault();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (nearest != null)
            {
                if (_manualSelectionBlocksAuto) return;

                double distance = userLoc.CalculateDistance(new Location(nearest.Latitude, nearest.Longitude), DistanceUnits.Kilometers) * 1000;
                if (distance <= 50)
                {
                    ShowInfoCard(nearest);

                    bool autoAudioEnabled = Preferences.Default.Get(AutoAudioKey, true);

                    if (!autoAudioEnabled)
                    {
                        // Chỉ dừng khi đang tự động phát (không chạm tới phát thủ công)
                        if (_isAutoPlaying)
                        {
                            AudioPlayer.Stop();
                            currentAudioPoiId = -1;
                            _isAutoPlaying = false;
                        }

                        return;
                    }

                    if (!string.IsNullOrEmpty(nearest.AudioUrl) && currentAudioPoiId != nearest.Id && CanPlay(nearest.Id))
                    {
                        currentAudioPoiId = nearest.Id;
                        _isAutoPlaying = true;

                        AudioPlayer.Stop();
                        AudioPlayer.Source = MediaSource.FromUri(nearest.AudioUrl);

                        await Task.Delay(500); // Đợi load nhẹ
                        AudioPlayer.Play();
                    }
                }
                else
                {
                    if (!_manualSelectionBlocksAuto)
                        HideCard();
                }
            }
        });
    }

    void ShowInfoCard(Poi poi)
    {
        _currentPoi = poi;
        PlaceName.Text = poi.Name;
        PlaceAddress.Text = poi.Address;
        PlaceHours.Text = poi.OpeningHours ?? "07:00 - 22:00";
        PlaceStatus.Text = "OPEN";
        PlaceImage.Source = string.IsNullOrEmpty(poi.ImageUrl) ? "logo.png" : poi.ImageUrl;
        InfoCard.IsVisible = true;
    }

    void HideCard()
    {
        InfoCard.IsVisible = false;
        AudioPlayer?.Stop();
        currentAudioPoiId = -1;
        _manualSelectionBlocksAuto = false;
        _isAutoPlaying = false;
    }

    void TryShowPinnedPoi()
    {
        var pinnedPoiId = Preferences.Default.Get(PinnedPoiKey, -1);
        if (pinnedPoiId <= 0 || poiList.Count == 0) return;

        var pinnedPoi = poiList.FirstOrDefault(p => p.Id == pinnedPoiId);
        if (pinnedPoi == null) return;

        _manualSelectionBlocksAuto = true;
        ShowInfoCard(pinnedPoi);
        MixiMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(pinnedPoi.Latitude, pinnedPoi.Longitude), Distance.FromMeters(400)));
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        AudioPlayer?.Stop();
        base.OnDisappearing();
    }

    void OnPlayClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null || string.IsNullOrWhiteSpace(_currentPoi.AudioUrl)) return;

        AudioPlayer.Stop();
        AudioPlayer.Source = MediaSource.FromUri(_currentPoi.AudioUrl);
        AudioPlayer?.Play();
        currentAudioPoiId = _currentPoi.Id;
        _manualSelectionBlocksAuto = false;
        _isAutoPlaying = false; // phát thủ công
    }

    void OnPauseClicked(object? sender, EventArgs e)
    {
        AudioPlayer?.Pause();
        _isAutoPlaying = false; // phát thủ công
    }

    async void OnGetLocationClicked(object? sender, EventArgs e)
    {
        var loc = await Geolocation.Default.GetLocationAsync();
        if (loc != null)
        {
            _currentUserLocation = loc;
            MixiMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(300)));
            ApplyFiltersAndRefreshMap();
        }
    }

    async void OnInfoCardTapped(object? sender, TappedEventArgs e)
    {
        if (_currentPoi?.Id <= 0) return;
        await Shell.Current.GoToAsync($"poidetail?poiId={_currentPoi.Id}");
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFiltersAndRefreshMap();
    }

    private void ApplyFiltersAndRefreshMap()
    {
        var source = poiList ?? new List<Poi>();
        var search = SearchEntry?.Text?.Trim() ?? string.Empty;

        IEnumerable<Poi> query = source;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (p.Address?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        filteredPoiList = query.ToList();
        RefreshMapPins(filteredPoiList);
    }

    private void RefreshMapPins(List<Poi> list)
    {
        MixiMap.Pins.Clear();
        MixiMap.MapElements.Clear();

        foreach (var poi in list)
        {
            var poiLocation = new Location(poi.Latitude, poi.Longitude);

            var pin = new Pin
            {
                Label = poi.Name,
                Address = poi.Address,
                Location = poiLocation,
                Type = PinType.Place
            };
            pin.MarkerClicked += (_, args) =>
            {
                args.HideInfoWindow = true;
                _currentPoi = poi;
                ShowInfoCard(poi);
                _manualSelectionBlocksAuto = true;
                Preferences.Default.Set(PinnedPoiKey, poi.Id);
                _isAutoPlaying = false;
                AudioPlayer.Stop();
                currentAudioPoiId = -1;
            };
            MixiMap.Pins.Add(pin);

            MixiMap.MapElements.Add(new Circle
            {
                Center = poiLocation,
                Radius = Distance.FromMeters(50),
                StrokeColor = Color.FromArgb("#E53935"),
                StrokeWidth = 2,
                FillColor = Color.FromRgba(229, 57, 53, 45)
            });
        }
    }
}