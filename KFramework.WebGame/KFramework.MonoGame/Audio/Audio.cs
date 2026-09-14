using KFramework.JSBind;

namespace KFramework;

/// <summary>
/// 极简音效系统：不依赖任何音频文件，全部由 WebAudio 振荡器 + 包络实时合成。
/// 优点：零资源体积、零加载时间；适合原型与网页小游戏。
/// 跨语言调用一律走 <see cref="AudioBind"/>，本类只做业务封装。
/// </summary>
public static class Audio
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
            JSBind_Audio.SetMuted(!value);
        }
    }

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (_enabled) JSBind_Audio.Play((int)sfx, volume, pitch);
    }

    /// <summary>浏览器要求用户手势后才能启动音频上下文，请在首次点击/按键时调用。</summary>
    public static void Unlock() => JSBind_Audio.Unlock();
}
