using System.Runtime.InteropServices.JavaScript;

namespace KFramework.JSBind
{

    /// <summary>
    /// audio 模块绑定。分两组能力：
    /// 1) 合成音效（振荡器实时合成，零资源，适合原型）；
    /// 2) 真实音频文件（wav/mp3/ogg，经 decodeAudioData 解码成 AudioBuffer 后播放）。
    /// 这里只做跨语言调用，业务封装见 KFramework.Audio 下的 SoundEffect / SoundEffectInstance / MediaPlayer。
    /// </summary>
    internal static partial class JSBind_Audio
    {
        // ===== 上下文 =====

        /// <summary>浏览器要求用户手势后才能启动音频上下文。</summary>
        [JSImport("unlock", "audio")]
        internal static partial void Unlock();

        [JSImport("setMuted", "audio")]
        internal static partial void SetMuted(bool muted);

        [JSImport("setMasterVolume", "audio")]
        internal static partial void SetMasterVolume(float volume);

        // ===== 音频缓冲 =====

        /// <summary>提交编码后的音频字节，JS 侧异步解码；用 IsLoaded 查询结果。</summary>
        [JSImport("loadAudio", "audio")]
        internal static partial void LoadAudio(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, string mime);

        [JSImport("isLoaded", "audio")]
        internal static partial bool IsLoaded(int handle);

        /// <summary>音频时长（秒）。未就绪时返回 0。</summary>
        [JSImport("getDuration", "audio")]
        internal static partial float GetDuration(int handle);

        [JSImport("releaseBuffer", "audio")]
        internal static partial void ReleaseBuffer(int handle);

        // ===== 播放实例 =====

        /// <summary>基于已解码的缓冲创建播放实例，返回实例 id；缓冲未就绪时返回 0。</summary>
        [JSImport("createInstance", "audio")]
        internal static partial int CreateInstance(int handle);

        [JSImport("playInstance", "audio")]
        internal static partial void PlayInstance(int instance, float volume, float pitch, float pan, bool loop);

        [JSImport("stopInstance", "audio")]
        internal static partial void StopInstance(int instance);

        [JSImport("pauseInstance", "audio")]
        internal static partial void PauseInstance(int instance);

        [JSImport("resumeInstance", "audio")]
        internal static partial void ResumeInstance(int instance);

        [JSImport("setInstanceVolume", "audio")]
        internal static partial void SetInstanceVolume(int instance, float volume);

        [JSImport("setInstancePitch", "audio")]
        internal static partial void SetInstancePitch(int instance, float pitch);

        [JSImport("setInstancePan", "audio")]
        internal static partial void SetInstancePan(int instance, float pan);

        [JSImport("setInstanceLoop", "audio")]
        internal static partial void SetInstanceLoop(int instance, bool loop);

        [JSImport("isInstancePlaying", "audio")]
        internal static partial bool IsInstancePlaying(int instance);

        [JSImport("releaseInstance", "audio")]
        internal static partial void ReleaseInstance(int instance);
    }
}
