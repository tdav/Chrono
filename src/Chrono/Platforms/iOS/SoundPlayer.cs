using AVFoundation;
using Chrono.Services;
using Foundation;

namespace Chrono.Platform;

/// <summary>Мелодия из бандла ({id}.wav) через AVAudioPlayer; категория Playback — звучит и в беззвучном режиме.</summary>
public sealed class SoundPlayer : ISoundPlayer
{
    private AVAudioPlayer? player;

    public void Play(string soundId, bool loop)
    {
        this.Stop();

        var url = NSBundle.MainBundle.GetUrlForResource(soundId, "wav");
        if (url is null)
        {
            return;
        }

        AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Playback);
        AVAudioSession.SharedInstance().SetActive(true);
        this.player = AVAudioPlayer.FromUrl(url, out _);
        if (this.player is null)
        {
            return;
        }

        this.player.NumberOfLoops = loop ? -1 : 0;
        this.player.Play();
    }

    public void Stop()
    {
        this.player?.Stop();
        this.player?.Dispose();
        this.player = null;
    }
}
