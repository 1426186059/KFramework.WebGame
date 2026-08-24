using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>基于 WebAudio 的音效与音频播放（JS 薄层封装）。</summary>
public static partial class Audio
{
    [JSImport("audio.init", "main.js")]
    public static partial void Init();

    /// <summary>确保 AudioContext 已创建并在用户手势后恢复。</summary>
    [JSImport("audio.ensure", "main.js")]
    public static partial void Ensure();

    /// <summary>播放一个合成音。waveType: sine / square / triangle / sawtooth。</summary>
    [JSImport("audio.beep", "main.js")]
    public static partial void Beep(float frequency, float duration, string waveType, float volume);

    /// <summary>预加载音频文件（mp3/wav/ogg）。播放时会自动加载，可不预调。</summary>
    [JSImport("audio.load", "main.js")]
    public static partial void Load(string url);

    /// <summary>播放音频文件。loop 是否循环；volume 0~1。</summary>
    [JSImport("audio.play", "main.js")]
    public static partial void Play(string url, bool loop = false, float volume = 1f);

    /// <summary>停止该 url 的播放（用于循环 BGM 或打断音效）。</summary>
    [JSImport("audio.stop", "main.js")]
    public static partial void Stop(string url);

    /// <summary>实时调整正在播放实例的音量（0~1）。</summary>
    [JSImport("audio.setVolume", "main.js")]
    public static partial void SetVolume(string url, float volume);
}
