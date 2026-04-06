using App.Models;
using App.Services;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;
using System.Threading;

namespace App.Views;

public partial class HomePage : ContentPage
{
    const string PinnedPoiKey = "pinnedPoiId";
    readonly ApiService api = ApiService.Instance;
    List<Poi> poiList = new();
    List<Poi> filteredPoiList = new();
    CancellationTokenSource? _cts;
    int currentAudioPoiId = -1;
    bool isFirstLocation = true;
    bool _isAutoPlaying = false;
    /// <summary>Đã bấm chọn POI trên map / ghim — thẻ treo dù ra khỏi bán kính cho đến khi Đóng hoặc kéo xuống.</summary>
    bool _cardPinnedByMapTap;
    Location? _currentUserLocation;
    CancellationTokenSource? _speechCts;
    Dictionary<int, DateTime> lastPlayed = new();
    const int COOLDOWN_SECONDS = 300;
    /// <summary>Độ lệch GPS tối đa (m) để tin cậy kích hoạt tự động.</summary>
    const double MaxGpsAccuracyMeters = 120;

    const string AutoAudioKey = "autoAudioEnabled";

    Poi? _currentPoi;
    /// <summary>Sau khi người dùng kéo tắt thẻ trong vùng — không tự bật lại cho POI này cho đến khi ra khỏi bán kính.</summary>
    int? _dismissedAutoCardForPoiId;
    double _infoCardPointerStartY;
    readonly AudioListenMeter _listenMeter;

    public HomePage()
    {
        InitializeComponent();
        _listenMeter = new AudioListenMeter(AudioPlayer, () => currentAudioPoiId);
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
                var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium), _cts.Token);
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

    static int EffectiveRadiusMeters(Poi p) => p.Radius > 0 ? p.Radius : 50;

    static double DistanceToPoiMeters(Location userLoc, Poi p) =>
        userLoc.CalculateDistance(new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers) * 1000;

    /// <summary>POI mà người dùng đang đứng trong bán kính (ưu tiên gần tâm nhất nếu trùng vùng).</summary>
    static Poi? FindPoiContainingUser(Location userLoc, List<Poi> pois)
    {
        return pois
            .Select(p => (Poi: p, DistM: DistanceToPoiMeters(userLoc, p)))
            .Where(x => x.DistM <= EffectiveRadiusMeters(x.Poi))
            .OrderBy(x => x.DistM)
            .Select(x => x.Poi)
            .FirstOrDefault();
    }

    private void CheckNearby(Location userLoc)
    {
        if (filteredPoiList == null || !filteredPoiList.Any()) return;

        // GPS kém → không tự phát / không tự đóng thẻ theo vùng (tránh nhầm quán xa)
        if (userLoc.Accuracy > 0 && userLoc.Accuracy > MaxGpsAccuracyMeters)
            return;

        var activePoi = FindPoiContainingUser(userLoc, filteredPoiList);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Đã bấm chọn POI: thẻ luôn treo; chỉ tự phát audio khi đang trong bán kính POI đó
            if (_cardPinnedByMapTap && _currentPoi != null)
            {
                ShowInfoCard(_currentPoi);

                bool insidePinned = DistanceToPoiMeters(userLoc, _currentPoi) <= EffectiveRadiusMeters(_currentPoi);
                bool autoAudioEnabled = Preferences.Default.Get(AutoAudioKey, true);

                if (!autoAudioEnabled || !insidePinned)
                {
                    if (_isAutoPlaying)
                    {
                        _listenMeter.StopAndFlushFireAndForget();
                        AudioPlayer.Stop();
                        currentAudioPoiId = -1;
                        _isAutoPlaying = false;
                    }

                    return;
                }

                if (_dismissedAutoCardForPoiId == _currentPoi.Id)
                    return;

                if (!string.IsNullOrEmpty(_currentPoi.AudioUrl) && currentAudioPoiId != _currentPoi.Id && CanPlay(_currentPoi.Id))
                {
                    currentAudioPoiId = _currentPoi.Id;
                    _isAutoPlaying = true;
                    await PlayPoiAudioAsync(_currentPoi);
                }

                return;
            }

            // Chưa bấm ghim: chỉ hiện thẻ khi đứng trong bán kính một POI; ra khỏi hết vùng thì ẩn
            if (activePoi == null)
            {
                if (_dismissedAutoCardForPoiId.HasValue)
                {
                    var dismissed = filteredPoiList.FirstOrDefault(p => p.Id == _dismissedAutoCardForPoiId.Value);
                    if (dismissed == null || DistanceToPoiMeters(userLoc, dismissed) > EffectiveRadiusMeters(dismissed))
                        _dismissedAutoCardForPoiId = null;
                }

                HideCard();
                return;
            }

            if (_dismissedAutoCardForPoiId != activePoi.Id)
                ShowInfoCard(activePoi);

            bool autoOn = Preferences.Default.Get(AutoAudioKey, true);

            if (!autoOn)
            {
                if (_isAutoPlaying)
                {
                    _listenMeter.StopAndFlushFireAndForget();
                    AudioPlayer.Stop();
                    currentAudioPoiId = -1;
                    _isAutoPlaying = false;
                }

                return;
            }

            if (_dismissedAutoCardForPoiId == activePoi.Id)
                return;

            if (!string.IsNullOrEmpty(activePoi.AudioUrl) && currentAudioPoiId != activePoi.Id && CanPlay(activePoi.Id))
            {
                currentAudioPoiId = activePoi.Id;
                _isAutoPlaying = true;
                await PlayPoiAudioAsync(activePoi);
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

    void HideCard(bool userSwipeDismiss = false)
    {
        if (userSwipeDismiss && _currentPoi != null)
            _dismissedAutoCardForPoiId = _currentPoi.Id;

        InfoCard.IsVisible = false;
        StopOfflineSpeech();
        _listenMeter.StopAndFlushFireAndForget();
        AudioPlayer?.Stop();
        currentAudioPoiId = -1;
        _currentPoi = null;
        _cardPinnedByMapTap = false;
        _isAutoPlaying = false;
        Preferences.Default.Remove(PinnedPoiKey);
    }

    void TryShowPinnedPoi()
    {
        var pinnedPoiId = Preferences.Default.Get(PinnedPoiKey, -1);
        if (pinnedPoiId <= 0 || poiList.Count == 0) return;

        var pinnedPoi = poiList.FirstOrDefault(p => p.Id == pinnedPoiId);
        if (pinnedPoi == null) return;

        _cardPinnedByMapTap = true;
        _dismissedAutoCardForPoiId = null;
        ShowInfoCard(pinnedPoi);
        MixiMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(pinnedPoi.Latitude, pinnedPoi.Longitude), Distance.FromMeters(400)));
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        StopOfflineSpeech();
        _listenMeter.StopAndFlushFireAndForget();
        AudioPlayer?.Stop();
        base.OnDisappearing();
    }

    async void OnPlayClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;

        await PlayPoiAudioAsync(_currentPoi);
        currentAudioPoiId = _currentPoi.Id;
        _isAutoPlaying = false; // phát thủ công
    }

    void OnPauseClicked(object? sender, EventArgs e)
    {
        StopOfflineSpeech();
        AudioPlayer?.Pause();
        _isAutoPlaying = false; // phát thủ công
    }

    private async Task PlayPoiAudioAsync(Poi poi)
    {
        StopOfflineSpeech();
        var hasInternet = NetworkReachability.HasUsableConnection;
        if (hasInternet && !string.IsNullOrWhiteSpace(poi.AudioUrl))
        {
            _listenMeter.StopAndFlushFireAndForget();
            AudioPlayer.Stop();
            AudioPlayer.Source = MediaSource.FromUri(poi.AudioUrl);
            await Task.Delay(300);
            AudioPlayer.Play();
            _listenMeter.Arm(Dispatcher, poi.Id);
            return;
        }

        var script = !string.IsNullOrWhiteSpace(poi.Description)
            ? poi.Description
            : $"{poi.Name}. {poi.Address}";

        _speechCts = new CancellationTokenSource();
        await TextToSpeech.Default.SpeakAsync(script, new SpeechOptions(), _speechCts.Token);
    }

    private void StopOfflineSpeech()
    {
        if (_speechCts == null) return;
        _speechCts.Cancel();
        _speechCts.Dispose();
        _speechCts = null;
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
        var selectedPoiId = _currentPoi?.Id;
        if (selectedPoiId is null or <= 0) return;
        await Shell.Current.GoToAsync($"poidetail?poiId={selectedPoiId.Value}");
    }

    void OnInfoCardPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType != GestureStatus.Completed)
            return;
        if (e.TotalY > 40)
            HideCard(userSwipeDismiss: true);
    }

    void OnInfoCardSwipedDown(object? sender, SwipedEventArgs e)
    {
        if (e.Direction == SwipeDirection.Down)
            HideCard(userSwipeDismiss: true);
    }

    void OnInfoCardPointerPressed(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(InfoCard);
        if (pos.HasValue)
            _infoCardPointerStartY = pos.Value.Y;
    }

    void OnInfoCardPointerReleased(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(InfoCard);
        if (!pos.HasValue)
            return;
        if (pos.Value.Y - _infoCardPointerStartY > 40)
            HideCard(userSwipeDismiss: true);
    }

    void OnInfoCardCloseTapped(object? sender, TappedEventArgs e) =>
        HideCard(userSwipeDismiss: true);

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
                _dismissedAutoCardForPoiId = null;
                _currentPoi = poi;
                ShowInfoCard(poi);
                _cardPinnedByMapTap = true;
                Preferences.Default.Set(PinnedPoiKey, poi.Id);
                _isAutoPlaying = false;
                _listenMeter.StopAndFlushFireAndForget();
                AudioPlayer.Stop();
                currentAudioPoiId = -1;
            };
            MixiMap.Pins.Add(pin);

            var r = Math.Max(EffectiveRadiusMeters(poi), 15);
            MixiMap.MapElements.Add(new Circle
            {
                Center = poiLocation,
                Radius = Distance.FromMeters(r),
                StrokeColor = Color.FromArgb("#2E7D32"),
                StrokeWidth = 2,
                FillColor = Color.FromRgba(46, 125, 50, 40)
            });
        }
    }
}