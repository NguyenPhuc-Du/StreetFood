using Plugin.Maui.Audio;

namespace App.Services;

public class AudioService
{
    readonly IAudioManager audioManager;
    IAudioPlayer? player;

    public AudioService(IAudioManager audioManager)
    {
        this.audioManager = audioManager;
    }

    public async Task Play(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;

            // Dừng cái cũ nếu đang phát
            Stop();

            var client = new HttpClient();
            var stream = await client.GetStreamAsync(url);

            player = audioManager.CreatePlayer(stream);
            player.Play();
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    public void Pause() => player?.Pause();
    public void Stop()
    {
        if (player != null)
        {
            player.Stop();
            player.Dispose();
            player = null;
        }
    }
}