namespace KFramework;

/// <summary>
/// 背景音乐播放器（对齐 MonoGame 的 Microsoft.Xna.Framework.Media.MediaPlayer）。
/// 简化自 MonoGame：直接播放一个循环 <see cref="SoundEffect"/>，不做 XACT/Song 库。
/// </summary>
public static class MediaPlayer
{
    private static SoundEffectInstance? _current;
    private static float _volume = 1f;
    private static bool _repeat;

    /// <summary>播放一段背景音乐（循环）。会先停止当前正在播放的曲目。</summary>
    public static void Play(SoundEffect song)
    {
        Stop();
        _current = song.CreateInstance();
        _current.IsLooped = _repeat;
        _current.Volume = _volume;
        _current.Play();
    }

    public static void Stop()
    {
        _current?.Stop();
        _current?.Dispose();
        _current = null;
    }

    public static void Pause()
    {
        if (_current is { State: SoundState.Playing }) _current.Pause();
    }

    public static void Resume()
    {
        if (_current is { State: SoundState.Paused }) _current.Resume();
    }

    public static float Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (_current is not null) _current.Volume = value;
        }
    }

    public static bool IsRepeating
    {
        get => _repeat;
        set
        {
            _repeat = value;
            if (_current is not null) _current.IsLooped = value;
        }
    }

    public static MediaState State => _current?.State switch
    {
        SoundState.Playing => MediaState.Playing,
        SoundState.Paused => MediaState.Paused,
        _ => MediaState.Stopped,
    };
}
