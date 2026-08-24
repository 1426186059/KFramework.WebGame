using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>基于 WebAudio 的简单音效（JS 薄层封装）。</summary>
public static partial class Audio
{
    [JSImport("audio.init", "main.js")]
    public static partial void Init();

    /// <summary>确保 AudioContext 已创建并在用户手势后恢复。</summary>
    [JSImport("audio.ensure", "main.js")]
    public static partial void Ensure();

    /// <summary>播放一个短音。waveType: sine / square / triangle / sawtooth。</summary>
    [JSImport("audio.beep", "main.js")]
    public static partial void Beep(double frequency, double duration, string waveType, double volume);
}
