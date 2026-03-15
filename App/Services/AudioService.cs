using Plugin.Maui.Audio;

namespace App.Services;

public class AudioService
{
    IAudioPlayer? player;

    public async Task Play(string url)
    {
        var stream = await new HttpClient().GetStreamAsync(url);

        player = AudioManager.Current.CreatePlayer(stream);

        player.Play();
    }

    public void Pause()
    {
        player?.Pause();
    }

    public void Stop()
    {
        player?.Stop();
    }
}