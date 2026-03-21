using App.Models;
using App.Services;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Media;

namespace App.Views;

[QueryProperty(nameof(PoiId), "poiId")]
public partial class PoiDetailPage : ContentPage
{
    const string PinnedPoiKey = "pinnedPoiId";
    private readonly ApiService _api = new();
    private readonly IDispatcherTimer _audioTimer;
    private bool _isSeeking;
    private PoiDetail? _currentDetail;
    private CancellationTokenSource? _speechCts;

    public int PoiId { get; set; }

    public ObservableCollection<FoodItem> Foods { get; } = new();

    public PoiDetailPage()
    {
        InitializeComponent();
        BindingContext = this;

        _audioTimer = Dispatcher.CreateTimer();
        _audioTimer.Interval = TimeSpan.FromMilliseconds(500);
        _audioTimer.Tick += (_, _) => UpdateAudioProgress();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var detail = await _api.GetPoiDetail(PoiId);
        if (detail == null) return;
        _currentDetail = detail;

        HeaderTitle.Text = detail.Name;
        PlaceName.Text = detail.Name;
        PlaceAddress.Text = $"Địa chỉ: {detail.Address ?? "-"}";
        PlacePhone.Text = $"Số điện thoại: {detail.Phone ?? "-"}";
        PlaceHours.Text = $"Giờ mở cửa: {detail.OpeningHours ?? "-"}";

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
        StopOfflineSpeech();
        _audioTimer.Stop();
        AudioPlayer?.Stop();
        base.OnDisappearing();
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
        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        if (hasInternet && !string.IsNullOrWhiteSpace(_currentDetail.AudioUrl))
        {
            AudioPlayer?.Play();
            _audioTimer.Start();
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

