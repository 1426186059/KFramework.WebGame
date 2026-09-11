using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 浏览器 Web Audio 音效后端封装。对应 tsengine/src/core/audio.ts
/// （mir.initAudio / playSound / stopSound / stopAllSounds / setSoundVolume）。
/// </summary>
public static partial class BrowserAudio
{
    [JSImport("mir.initAudio", "main.js")]
    private static partial void InitAudioImpl();

    [JSImport("mir.playSound", "main.js")]
    private static partial int PlaySoundImpl(string url, int volume, bool loop);

    [JSImport("mir.stopSound", "main.js")]
    private static partial void StopSoundImpl(int id);

    [JSImport("mir.stopAllSounds", "main.js")]
    private static partial void StopAllSoundsImpl();

    [JSImport("mir.setSoundVolume", "main.js")]
    private static partial void SetSoundVolumeImpl(int id, int volume);

    public static void InitAudio() => InitAudioImpl();

    /// <summary>
    /// 播放一段音效。url 为游戏内相对路径（如 ".\Sound\1.wav"），
    /// 这里统一按资源基址解析成可请求的 URL，与 BrowserResource.GetBytes 保持一致。
    /// </summary>
    public static int PlaySound(string url, int volume, bool loop)
        => PlaySoundImpl(BrowserResource.ResolveUrl(url), volume, loop);
    public static void StopSound(int id) => StopSoundImpl(id);
    public static void StopAllSounds() => StopAllSoundsImpl();
    public static void SetSoundVolume(int id, int volume) => SetSoundVolumeImpl(id, volume);
}
