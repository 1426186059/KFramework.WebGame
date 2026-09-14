namespace KFramework.MonoGame;

/// <summary>
/// 引擎内置合成音效：不依赖任何音频文件，全部由 WebAudio 振荡器 + 包络实时合成。
/// 优点：零资源体积、零加载时间，适合原型与网页小游戏。
/// 合成映射（每个 <see cref="Sfx"/> 对应哪些 tone/noise 及参数）在本类中以 C# 实现，
/// JS 侧仅保留薄原语 playTone / playNoise（见 JSBind_Audio）。
/// 需要播放真实音频文件（wav/mp3/ogg）时，改用 SoundEffect / MediaPlayer。
/// </summary>
public static class SynthAudio
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

    /// <summary>是否启用音效（联动引擎 master 静音，影响全部声音）。</summary>
    public static bool Enabled
    {
        get => !AudioMaster.IsMuted;
        set => AudioMaster.IsMuted = !value;
    }

    /// <summary>播放一个内置合成音效。</summary>
    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (AudioMaster.IsMuted) return;
        float v = Math.Clamp(volume, 0f, 1f);
        float p = pitch > 0f ? pitch : 1f;
        switch (sfx)
        {
            case Sfx.Shoot:
                Tone("square", 880f * p, 220f * p, 0.10f, 0.16f * v, 0f);
                break;
            case Sfx.Explosion:
                Noise(0.45f, 0.55f * v, 1800f, 90f);
                Tone("sawtooth", 180f * p, 40f * p, 0.35f, 0.14f * v, 0f);
                break;
            case Sfx.Hit:
                Tone("triangle", 420f * p, 180f * p, 0.09f, 0.18f * v, 0f);
                break;
            case Sfx.Pickup:
                Tone("sine", 660f * p, 660f * p, 0.07f, 0.20f * v, 0f);
                Tone("sine", 880f * p, 880f * p, 0.07f, 0.20f * v, 0.06f);
                Tone("sine", 1170f * p, 1170f * p, 0.10f, 0.18f * v, 0.12f);
                break;
            case Sfx.Select:
                Tone("sine", 520f * p, 780f * p, 0.08f, 0.16f * v, 0f);
                break;
            case Sfx.GameOver:
                Tone("sawtooth", 420f * p, 60f * p, 0.90f, 0.22f * v, 0f);
                Tone("square", 210f * p, 40f * p, 1.00f, 0.12f * v, 0.05f);
                break;
            case Sfx.PowerUp:
                Tone("square", 440f * p, 440f * p, 0.08f, 0.16f * v, 0f);
                Tone("square", 587f * p, 587f * p, 0.08f, 0.16f * v, 0.07f);
                Tone("square", 880f * p, 880f * p, 0.14f, 0.18f * v, 0.14f);
                break;
        }
    }

    /// <summary>浏览器要求用户手势后才能启动音频上下文，请在首次点击/按键时调用。</summary>
    public static void Unlock() => AudioMaster.Unlock();

    private static void Tone(string type, float from, float to, float duration, float volume, float delay)
        => JSBind_Audio.PlayTone(type, from, to, duration, volume, delay);

    private static void Noise(float duration, float volume, float cutoffFrom, float cutoffTo)
        => JSBind_Audio.PlayNoise(duration, volume, cutoffFrom, cutoffTo);
}
