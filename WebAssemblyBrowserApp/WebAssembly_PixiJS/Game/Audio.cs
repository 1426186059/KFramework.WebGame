using System.Runtime.InteropServices.JavaScript;

namespace PixiGame;

/// <summary>音效（WebAudio 合成，JSImport 到 core/audio.js）。</summary>
public static partial class Audio
{
    [JSImport("audio.init", "main.js")] public static partial void Init();

    /// <summary>播放一个音调。waveType: sine|square|sawtooth|triangle。</summary>
    [JSImport("audio.beep", "main.js")]
    public static partial void Beep(double frequency, double durationSec, string waveType, double volume);
}
