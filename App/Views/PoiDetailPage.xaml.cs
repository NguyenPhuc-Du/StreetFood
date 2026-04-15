using System.Collections.Generic;
using App.Models;
using App.Services;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;

namespace App.Views;

/// <summary>
/// Dùng IQueryAttributable để đọc poiId trước khi tải API.
/// [QueryProperty] đôi khi gán sau OnAppearing → PoiId = 0 → không có dữ liệu hiển thị.
/// </summary>
public partial class PoiDetailPage : ContentPage, IQueryAttributable
{
    const string PinnedPoiKey = "pinnedPoiId";
    private readonly ApiService _api = ApiService.Instance;
    private readonly AudioListenMeter _listenMeter;
    private readonly IDispatcherTimer _audioTimer;
    private bool _isSeeking;
    private PoiDetail? _currentDetail;
    private CancellationTokenSource? _speechCts;
    private int _lastLoadedPoiId;

    public int PoiId { get; set; }

    public ObservableCollection<FoodItem> Foods { get; } = new();

    public PoiDetailPage()
    {
        InitializeComponent();
        BindingContext = this;
        _listenMeter = new AudioListenMeter(AudioPlayer, () => PoiId);
        ApplyLocalizedTexts();

        _audioTimer = Dispatcher.CreateTimer();
        _audioTimer.Interval = TimeSpan.FromMilliseconds(500);
        _audioTimer.Tick += (_, _) => UpdateAudioProgress();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!TryParsePoiId(query, out var id)) return;
        PoiId = id;
        // Một số phiên bản Shell gọi ApplyQueryAttributes sau OnAppearing — buộc tải lại khi vừa gán poiId.
        _ = MainThread.InvokeOnMainThreadAsync(async () => await LoadDetailAsync());
    }

    static bool TryParsePoiId(IDictionary<string, object> query, out int id)
    {
        id = 0;
        foreach (var key in new[] { "poiId", "PoiId" })
        {
            if (!query.TryGetValue(key, out var raw)) continue;
            var s = raw?.ToString();
            if (int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id) && id > 0)
                return true;
        }
        return false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedTexts();
        await LoadDetailAsync();
    }

    async Task LoadDetailAsync()
    {
        if (PoiId <= 0) return;
        if (_lastLoadedPoiId == PoiId && _currentDetail != null) return;

        var detail = await _api.GetPoiDetail(PoiId, forceRefresh: true);
        if (detail == null) return;

        _lastLoadedPoiId = PoiId;
        _currentDetail = detail;

        HeaderTitle.Text = detail.Name;
        PlaceName.Text = detail.Name;

        if (!string.IsNullOrWhiteSpace(detail.Description))
        {
            PlaceDescription.Text = detail.Description;
            PlaceDescription.IsVisible = true;
        }
        else
        {
            PlaceDescription.Text = string.Empty;
            PlaceDescription.IsVisible = false;
        }

        PlaceAddress.Text = $"{LocalizationService.T("Address")}: {ResolveAddress(detail.Address, detail.Description)}";
        PlacePhone.Text = $"{LocalizationService.T("PoiPhone")}: {(string.IsNullOrWhiteSpace(detail.Phone) ? "—" : detail.Phone.Trim())}";
        PlaceHours.Text = $"{LocalizationService.T("PoiOpenHours")}: {(string.IsNullOrWhiteSpace(detail.OpeningHours) ? "—" : detail.OpeningHours.Trim())}";

        PlaceImage.Source = string.IsNullOrEmpty(detail.ImageUrl) ? "logo.png" : detail.ImageUrl;

        Foods.Clear();
        foreach (var food in detail.Foods ?? new List<FoodItem>())
            Foods.Add(food);

        if (!string.IsNullOrEmpty(detail.AudioUrl))
        {
            AudioPlayer.Source = MediaSource.FromUri(detail.AudioUrl);
        }

        ResetAudioProgress();
    }

    static string ResolveAddress(string? address, string? description)
    {
        return string.IsNullOrWhiteSpace(address) ? "—" : address.Trim();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnPlayClicked(object? sender, EventArgs e)
    {
        _ = PlayDetailAudioAsync();
    }

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        StopOfflineSpeech();
        AudioPlayer?.Pause();
        _audioTimer.Stop();
    }

    protected override void OnDisappearing()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        StopOfflineSpeech();
        _audioTimer.Stop();
        _listenMeter.StopAndFlushFireAndForget();
        AudioPlayer?.Stop();
        base.OnDisappearing();
    }

    void OnLanguageChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplyLocalizedTexts();
            if (_currentDetail != null)
            {
                PlaceAddress.Text = $"{LocalizationService.T("Address")}: {ResolveAddress(_currentDetail.Address, _currentDetail.Description)}";
                PlacePhone.Text = $"{LocalizationService.T("PoiPhone")}: {(string.IsNullOrWhiteSpace(_currentDetail.Phone) ? "—" : _currentDetail.Phone.Trim())}";
                PlaceHours.Text = $"{LocalizationService.T("PoiOpenHours")}: {(string.IsNullOrWhiteSpace(_currentDetail.OpeningHours) ? "—" : _currentDetail.OpeningHours.Trim())}";
            }
        });
    }

    void ApplyLocalizedTexts()
    {
        if (_currentDetail == null)
            HeaderTitle.Text = LocalizationService.T("PoiHeaderDetails");
        FoodsHeaderLabel.Text = LocalizationService.T("PoiFoods");
        AudioSectionLabel.Text = LocalizationService.T("PoiAudio");
        PlayButton.Text = LocalizationService.T("Play");
        PauseButton.Text = LocalizationService.T("Pause");
        NavigateButton.Text = LocalizationService.T("PoiNavigate");
    }

    private void OnAudioProgressSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isSeeking || AudioPlayer is null) return;

        var duration = AudioPlayer.Duration;
        if (duration <= TimeSpan.Zero) return;

        var target = TimeSpan.FromSeconds(e.NewValue);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (target > duration) target = duration;
        AudioPlayer.SeekTo(target);
        CurrentTimeLabel.Text = FormatTime(target);
    }

    private void UpdateAudioProgress()
    {
        if (AudioPlayer is null) return;

        var duration = AudioPlayer.Duration;
        var position = AudioPlayer.Position;

        if (duration <= TimeSpan.Zero)
        {
            TotalTimeLabel.Text = "00:00";
            CurrentTimeLabel.Text = "00:00";
            AudioProgressSlider.Maximum = 1;
            AudioProgressSlider.Value = 0;
            return;
        }

        _isSeeking = true;
        AudioProgressSlider.Maximum = duration.TotalSeconds;
        AudioProgressSlider.Value = Math.Min(position.TotalSeconds, duration.TotalSeconds);
        _isSeeking = false;

        CurrentTimeLabel.Text = FormatTime(position);
        TotalTimeLabel.Text = FormatTime(duration);
    }

    private void ResetAudioProgress()
    {
        CurrentTimeLabel.Text = "00:00";
        TotalTimeLabel.Text = "00:00";
        AudioProgressSlider.Maximum = 1;
        AudioProgressSlider.Value = 0;
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value.TotalHours >= 1)
            return value.ToString(@"hh\:mm\:ss");
        return value.ToString(@"mm\:ss");
    }

    private async void OnNavigateClicked(object? sender, EventArgs e)
    {
        if (_currentDetail == null) return;

        Preferences.Default.Set(PinnedPoiKey, _currentDetail.Id);

        var options = new MapLaunchOptions
        {
            Name = _currentDetail.Name,
            NavigationMode = NavigationMode.Driving
        };

        await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(
            new Location(_currentDetail.Latitude, _currentDetail.Longitude),
            options);
    }

    private async Task PlayDetailAudioAsync()
    {
        if (_currentDetail == null) return;

        StopOfflineSpeech();
        var hasInternet = NetworkReachability.HasUsableConnection;
        if (hasInternet && !string.IsNullOrWhiteSpace(_currentDetail.AudioUrl))
        {
            AudioPlayer?.Play();
            _audioTimer.Start();
            _listenMeter.Arm(Dispatcher, PoiId);
            return;
        }

        _audioTimer.Stop();
        var script = !string.IsNullOrWhiteSpace(_currentDetail.Description)
            ? _currentDetail.Description
            : $"{_currentDetail.Name}. {_currentDetail.Address}";
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
}

