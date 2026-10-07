using Android.Media;
using Chrono.Services;

namespace Chrono.Platform;

/// <summary>Мелодия из assets ({id}.wav) через поток будильника; при ошибке — системный звук будильника.</summary>
public sealed class SoundPlayer : ISoundPlayer
{
    private MediaPlayer? player;

    public void Play(string soundId, bool loop)
    {
        this.Stop();

        var context = global::Android.App.Application.Context;
        var player = new MediaPlayer();
        player.SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Alarm)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!);
        try
        {
            using var fd = context.Assets!.OpenFd($"{soundId}.wav");
            player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
            player.Prepare();
        }
        catch (Exception)
        {
            // Мелодия недоступна — играем системный звук будильника, чтобы сигнал не пропал.
            player.Reset();
            player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Alarm)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!);
            player.SetDataSource(context, RingtoneManager.GetDefaultUri(RingtoneType.Alarm)!);
            player.Prepare();
        }

        player.Looping = loop;
        player.Start();
        this.player = player;
    }

    public void Stop()
    {
        if (this.player is null)
        {
            return;
        }

        this.player.Stop();
        this.player.Release();
        this.player = null;
    }
}
