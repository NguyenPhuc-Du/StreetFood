using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace App.Services;

/// <summary>Đếm giây phát MediaElement (mỗi giây chỉ khi đang Playing và POI khớp).</summary>
public sealed class AudioListenMeter
{
    readonly MediaElement _player;
    readonly Func<int> _currentPoiId;
    IDispatcherTimer? _timer;
    int _armedPoiId;
    int _seconds;

    public AudioListenMeter(MediaElement player, Func<int> currentPoiId)
    {
        _player = player;
        _currentPoiId = currentPoiId;
    }

    public void Arm(IDispatcher dispatcher, int poiId)
    {
        StopAndFlushFireAndForget();
        _armedPoiId = poiId;
        _seconds = 0;
        _timer ??= dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        if (!_timer.IsRunning)
            _timer.Start();
    }

    void OnTick(object? sender, EventArgs e)
    {
        if (_armedPoiId <= 0) return;
        if (_currentPoiId() != _armedPoiId) return;
        if (_player.CurrentState != MediaElementState.Playing) return;
        _seconds++;
    }

    public void StopAndFlushFireAndForget()
    {
        _timer?.Stop();
        var pid = _armedPoiId;
        var sec = _seconds;
        _armedPoiId = 0;
        _seconds = 0;
        if (pid > 0 && sec >= 3)
            ListenTelemetry.ReportFireAndForget(pid, sec);
    }
}
