using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// audio 模块绑定。分两组能力：
    /// 1) 合成音效（振荡器实时合成，零资源，适合原型）；
    /// 2) 真实音频文件（wav/mp3/ogg，经 decodeAudioData 解码成 AudioBuffer 后播放）。
    /// 依赖 KFramework.TSEngine 项目（TypeScript 源码）：本类所有 JSImport 映射到 src/audio.ts 的 "audio" 模块；
/// 编译产物 audio.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
/// 这里只做跨语言调用，业务封装见 KFramework.MonoGame.Audio 下的 SoundEffect / SoundEffectInstance / MediaPlayer。
    /// </summary>
    public static partial class JSBind_Audio
    {
        // ===== 上下文 =====

        /// <summary>浏览器要求用户手势后才能启动音频上下文。</summary>
        [JSImport("unlock", "audio")]
        public static partial void Unlock();

        /// <summary>静音开关（整条音频输出）。</summary>
        [JSImport("setMuted", "audio")]
        public static partial void SetMuted(bool muted);

        /// <summary>设置主音量（0~1）。</summary>
        [JSImport("setMasterVolume", "audio")]
        public static partial void SetMasterVolume(float volume);

        // ===== 合成音效（薄原语；合成映射在 C# 层 SynthAudio） =====

        /// <summary>单振荡器音：频率从 from 滑到 to，时长 duration，增益包络 volume，delay 秒后开始。</summary>
        [JSImport("playTone", "audio")]
        public static partial void PlayTone(string type, float from, float to, float duration, float volume, float delay);

        /// <summary>滤波噪声：低通截止从 cutoffFrom 滑到 cutoffTo，时长 duration，增益 volume。</summary>
        [JSImport("playNoise", "audio")]
        public static partial void PlayNoise(float duration, float volume, float cutoffFrom, float cutoffTo);

        // ===== 音频缓冲 =====

        /// <summary>提交编码后的音频字节，JS 侧异步解码；用 IsLoaded 查询结果。</summary>
        [JSImport("loadAudio", "audio")]
        public static partial void LoadAudio(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, string mime);

        [JSImport("isLoaded", "audio")]
        public static partial bool IsLoaded(int handle);

        /// <summary>音频时长（秒）。未就绪时返回 0。</summary>
        [JSImport("getDuration", "audio")]
        public static partial float GetDuration(int handle);

        [JSImport("releaseBuffer", "audio")]
        public static partial void ReleaseBuffer(int handle);

        // ===== 播放实例 =====

        /// <summary>基于已解码的缓冲创建播放实例，返回实例 id；缓冲未就绪时返回 0。</summary>
        [JSImport("createInstance", "audio")]
        public static partial int CreateInstance(int handle);

        /// <summary>播放缓冲实例（音量/音高/声像/循环）。</summary>
        [JSImport("playInstance", "audio")]
        public static partial void PlayInstance(int instance, float volume, float pitch, float pan, bool loop);

        /// <summary>停止实例（回到开头，可再次 play）。</summary>
        [JSImport("stopInstance", "audio")]
        public static partial void StopInstance(int instance);

        /// <summary>暂停实例（保留播放位置）。</summary>
        [JSImport("pauseInstance", "audio")]
        public static partial void PauseInstance(int instance);

        /// <summary>从暂停处继续播放。</summary>
        [JSImport("resumeInstance", "audio")]
        public static partial void ResumeInstance(int instance);

        /// <summary>设置实例音量。</summary>
        [JSImport("setInstanceVolume", "audio")]
        public static partial void SetInstanceVolume(int instance, float volume);

        /// <summary>设置实例音高（播放速率）。</summary>
        [JSImport("setInstancePitch", "audio")]
        public static partial void SetInstancePitch(int instance, float pitch);

        /// <summary>设置实例声像（-1 左 / 0 中 / 1 右）。</summary>
        [JSImport("setInstancePan", "audio")]
        public static partial void SetInstancePan(int instance, float pan);

        /// <summary>设置实例是否循环。</summary>
        [JSImport("setInstanceLoop", "audio")]
        public static partial void SetInstanceLoop(int instance, bool loop);

        /// <summary>实例是否正在播放。</summary>
        [JSImport("isInstancePlaying", "audio")]
        public static partial bool IsInstancePlaying(int instance);

        /// <summary>释放实例（与 CreateInstance 配对）。</summary>
        [JSImport("releaseInstance", "audio")]
        public static partial void ReleaseInstance(int instance);
    }
}
