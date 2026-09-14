using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Example2;

/// <summary>
/// 本示例自带的合成音效：不依赖任何音频文件，由 WebAudio 实时合成。
/// 它的存在说明 KFramework.MonoGame 引擎本身并不绑定合成逻辑——合成只是“例子自己的实现”，
/// 通过 [JSImport] 直接调用引擎 JS 模块 audio 的 playSynth。
/// 真正需要资源化的战斗音效请见 <see cref="SoundCenter"/>（SoundEffect 加载真实 wav）。
/// </summary>
public static partial class SynthAudio
{
    /// <summary>内置合成音色。</summary>
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

    [JSImport("unlock", "audio")]
    private static partial void JsUnlock();

    [JSImport("playSynth", "audio")]
    private static partial void JsPlaySynth(int kind, float volume, float pitch);

    /// <summary>浏览器要求用户手势后才能启动音频上下文，请在首次点击/按键时调用。</summary>
    public static void Unlock() => JsUnlock();

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        => JsPlaySynth((int)sfx, volume, pitch);
}
