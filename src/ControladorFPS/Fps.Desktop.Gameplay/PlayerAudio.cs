using System;
using System.IO;
using Raylib_cs;

namespace Fps.Desktop.Gameplay;

internal sealed class PlayerAudio : IDisposable
{
    private Sound fallVoice;
    private bool hasFallVoice;

    public PlayerAudio()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "audio", "player",
            "voz ao cair de lugar alto.mp3");
        if (!Raylib.IsAudioDeviceReady() || !File.Exists(path))
            return;

        fallVoice = Raylib.LoadSound(path);
        hasFallVoice = fallVoice.FrameCount > 0;
    }

    public void PlayHighFallVoice()
    {
        if (hasFallVoice && !Raylib.IsSoundPlaying(fallVoice))
            Raylib.PlaySound(fallVoice);
    }

    public void Dispose()
    {
        if (hasFallVoice)
            Raylib.UnloadSound(fallVoice);
    }
}
