using KFramework.MonoGame;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 全局音频控制：对应浏览器 AudioContext 的全局状态（解锁 / 主音量 / 静音）。
    /// 与 MonoGame 不同，浏览器要求用户手势后才能启动音频上下文，故提供 <see cref="Unlock"/>。
    /// 真实音效经 <see cref="SoundEffect"/> / <see cref="MediaPlayer"/> 播放，
    /// 上层若自行封装合成音效，也统一受本控制影响（JS 侧所有声音都经 master 增益节点）。
    /// </summary>
    public static class AudioMaster
    {
        private static bool _muted;
        private static float _volume = 1f;

        /// <summary>是否静音（影响全部声音）。</summary>
        public static bool IsMuted
        {
            get => _muted;
            set
            {
                _muted = value;
                JSBind_Audio.SetMuted(value);
            }
        }

        /// <summary>主音量（0~1，影响全部声音）。</summary>
        public static float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                JSBind_Audio.SetMasterVolume(_volume);
            }
        }

        /// <summary>浏览器要求用户手势后才能启动音频上下文，请在首次点击/按键时调用。</summary>
        public static void Unlock() => JSBind_Audio.Unlock();
    }
}
