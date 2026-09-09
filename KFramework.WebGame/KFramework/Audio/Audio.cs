using System.Runtime.InteropServices.JavaScript;

namespace KFramework;

/// <summary>
/// 极简音效系统：不依赖任何音频文件，全部由 WebAudio 振荡器 + 包络实时合成。
/// 优点：零资源体积、零加载时间；适合原型与网页小游戏。
/// </summary>
public static partial class Audio
{
    /// <summary>内置音色。</summary>
    public enum Sfx : int
    {
        Shoot = 0,
        Explosion = 1,
        Hit = 2,
        Pickup = 3,
        Select = 4,
        GameOver = 5,
        PowerUp = 6,
    }

    private static bool _enabled = true;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            SetMutedCore(!value);
        }
    }

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (_enabled) PlayCore((int)sfx, volume, pitch);
    }

    /// <summary>浏览器要求用户手势后才能启动音频上下文，请在首次点击/按键时调用。</summary>
    public static void Unlock() => UnlockCore();

    [JSImport("audio.play", "audio")]
    private static partial void PlayCore(int sfx, float volume, float pitch);

    [JSImport("audio.unlock", "audio")]
    private static partial void UnlockCore();

    [JSImport("audio.setMuted", "audio")]
    private static partial void SetMutedCore(bool muted);
}
